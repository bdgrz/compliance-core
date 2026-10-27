using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.AccessControl.Readers;

sealed class FitzAccessGrantDirectory(IKvClient client, IDomainEventReader events)
    : FitzKvProjectionStore(client, AccessGrantProjectionKeys.Route, AccessGrantProjectionKeys.Projector),
      IAccessGrantDirectory, IAccessGrantProjection
{
    public async ValueTask<AccessGrantSetView> ListAsync(Uuid tenantId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var stored = await tx.GetAsync(AccessGrantProjectionKeys.State, ct).ConfigureAwait(false);
        if (!stored.Found)
            return new AccessGrantSetView(tenantId, 0, []);
        var state = JsonSerializer.Deserialize(stored.Value!.Value.Span,
            ComplianceCoreJsonContext.Default.AccessGrantProjectionState);
        return state?.ToView(tenantId) ??
               throw new InvalidOperationException("The access grant projection state could not be read.");
    }

    public async ValueTask<AccessGrantView?> GetAsync(Uuid tenantId, Uuid grantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var stored = await tx.GetAsync(AccessGrantProjectionKeys.State, ct).ConfigureAwait(false);
        if (!stored.Found)
            return null;
        var state = JsonSerializer.Deserialize(stored.Value!.Value.Span,
            ComplianceCoreJsonContext.Default.AccessGrantProjectionState);
        if (state is null)
            throw new InvalidOperationException("The access grant projection state could not be read.");
        return state.ToView(tenantId).Grants.SingleOrDefault(item => item.GrantId == grantId);
    }

    public async ValueTask<Uuid?> GetMembershipEpisodeIdAsync(Uuid tenantId, Uuid grantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var stored = await tx.GetAsync(AccessGrantProjectionKeys.State, ct).ConfigureAwait(false);
        if (!stored.Found)
            return null;
        var state = JsonSerializer.Deserialize(stored.Value!.Value.Span,
            ComplianceCoreJsonContext.Default.AccessGrantProjectionState);
        if (state is null)
            throw new InvalidOperationException("The access grant projection state could not be read.");
        return state.GetMembershipEpisodeId(grantId);
    }

    public async ValueTask<IReadOnlySet<Uuid>> FindPendingRevocationsAsync(Uuid tenantId,
        IReadOnlySet<Uuid> grantIds, CancellationToken ct = default)
    {
        if (grantIds.Count == 0)
            return new HashSet<Uuid>();

        var pattern = EventStreamPattern.ForPattern(tenantId.ToString());
        var checkpoint = await base.LoadCheckpointAsync(new CheckpointIdentity(
                AccessGrantProjectionKeys.Projector, pattern), ct)
            .ConfigureAwait(false);
        var pending = new HashSet<Uuid>();
        await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor, ct)
                           .ConfigureAwait(false))
        {
            if (record.Event is AccessGrantRevoked revoked && revoked.TenantId == tenantId &&
                grantIds.Contains(revoked.GrantId))
                pending.Add(revoked.GrantId);
        }
        return pending;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is not AccessGrantIssued and not AccessGrantRevoked)
            return;

        var stored = await Transaction.GetAsync(AccessGrantProjectionKeys.State, ct).ConfigureAwait(false);
        var state = stored.Found
            ? JsonSerializer.Deserialize(stored.Value!.Value.Span,
                ComplianceCoreJsonContext.Default.AccessGrantProjectionState)
            : new AccessGrantProjectionState();
        if (state is null)
            throw new InvalidOperationException("The access grant projection state could not be read.");

        var priorRevision = state.Revision;
        state.Apply(domainEvent);
        if (state.Revision == priorRevision)
            return;

        var json = JsonSerializer.SerializeToUtf8Bytes(state,
            ComplianceCoreJsonContext.Default.AccessGrantProjectionState);
        await Transaction.PutAsync(AccessGrantProjectionKeys.State, json, ct).ConfigureAwait(false);
    }

}
