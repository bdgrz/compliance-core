using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Policies.Contracts;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Events;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Policies.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Policies.Readers;

sealed class FitzPolicyDirectory(IKvClient client, PolicyReader policies)
    : FitzKvProjectionStore(client, "kv://bdgrz/policy-directory-v2/projection", ProjectorName),
        IPolicyDirectoryReader, IPolicyDirectoryProjection
{
    public const string ProjectorName = "PolicyDirectoryV2";
    Uuid? _batchTenantId;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context,
        CancellationToken ct = default) => BeginPolicyBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context,
        CancellationToken ct) => BeginPolicyBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginPolicyBatchAsync(ProjectionBatchContext context,
        CancellationToken ct)
    {
        if (context.Identity.Pattern.Area != "policies" || context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) || tenantId == Uuid.Empty)
            throw new InvalidOperationException("A policy directory batch requires its tenant's policy area.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "policies")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is not IPolicyFact fact || fact.Header is not { } header ||
            fact.Operation is null || _batchTenantId != header.TenantId || fact.TenantId != header.TenantId ||
            header.ProgramId == Uuid.Empty || header.PolicyId == Uuid.Empty || header.Revision < 1 ||
            fact.NativeSequence < 1 || header.PolicyId != Policy.IdFor(header.TenantId, header.ProgramId, header.Identifier) ||
            fact.Operation.Owner != new EventStreamAddress(header.TenantId.ToString(), "policies", header.PolicyId.ToString()))
            throw new InvalidOperationException("A policy directory fact requires exact native owner and batch scope.");
        var scope = domainEvent switch
        {
            PolicyDraftCreated value => (value.TenantId, value.ProgramId, value.PolicyId),
            PolicyDraftRevised value => (value.TenantId, value.ProgramId, value.PolicyId),
            PolicyReviewed value => (value.TenantId, value.ProgramId, value.PolicyId),
            PolicyApproved value => (value.TenantId, value.ProgramId, value.PolicyId),
            PolicyPeriodicReviewConfirmed value => (value.TenantId, value.ProgramId, value.PolicyId),
            PolicyRetirementProposed value => (value.TenantId, value.ProgramId, value.PolicyId),
            PolicyRetired value => (value.TenantId, value.ProgramId, value.PolicyId),
            PolicyDraftDiscarded value => (value.TenantId, value.ProgramId, value.PolicyId),
            _ => (Uuid.Empty, Uuid.Empty, Uuid.Empty),
        };
        if (scope != (header.TenantId, header.ProgramId, header.PolicyId))
            throw new InvalidOperationException("The policy directory metadata differs from its native owner scope.");
        var current = await PolicyDirectorySchema.Policies.GetAsync(Transaction, header.PolicyId, ct).ConfigureAwait(false);
        var next = new PolicyDirectoryRow(header.TenantId, header.ProgramId, header.PolicyId, header.Identifier,
            header, fact.NativeSequence);
        if (current is null)
        {
            if (domainEvent is not PolicyDraftCreated || header.Revision != 1 || fact.NativeSequence != 1)
                throw new InvalidOperationException("A policy cannot project before its exact native creation.");
            await PolicyDirectorySchema.Policies.InsertAsync(Transaction, next, ct).ConfigureAwait(false);
            return;
        }
        if (current.TenantId != header.TenantId || current.ProgramId != header.ProgramId || current.Identifier != header.Identifier)
            throw new InvalidOperationException("A policy directory identity cannot change its original tenant/program/identifier.");
        if (fact.NativeSequence < current.NativeSequence)
            return;
        if (fact.NativeSequence == current.NativeSequence)
        {
            if (current != next)
                throw new InvalidOperationException("A replayed policy directory fact cannot replace its original native cut.");
            return;
        }
        if (domainEvent is PolicyDraftCreated || !current.Header.IsVisible ||
            fact.NativeSequence != current.NativeSequence + 1 || header.Revision < current.Header.Revision ||
            header.Revision > current.Header.Revision + 1)
            throw new InvalidOperationException("A policy mutation must follow its exact native predecessor.");
        await PolicyDirectorySchema.Policies.ReplaceAsync(Transaction, current, next, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<PolicySummaryView>> ListProgramAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        Page<PolicyDirectoryRow> page;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            page = await PolicyDirectorySchema.Policies.QueryAsync(tx,
                PolicyDirectorySchema.ByProgramIdentifier.Query().WithPrefix(programId.ToString())
                    .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
        var values = new List<PolicySummaryView>(page.Items.Count);
        foreach (var row in page.Items)
        {
            if (row.TenantId != tenantId || row.ProgramId != programId)
                throw new ImmutableContentIntegrityException("The policy directory row has another tenant/program scope.");
            var source = await policies.ReadAsync(tenantId, programId, row.PolicyId, ct).ConfigureAwait(false);
            if (source.Owner.Header != row.Header || source.Owner.NativeSequence != row.NativeSequence)
                throw new ImmutableContentUnavailableException("The policy discovery projection has not reached its exact native source.");
            if (!source.IsVisible)
                continue;
            var view = source.ToView(DateOnly.MinValue);
            var title = source.CurrentVersion?.Content.Title ?? source.DraftContent?.Title;
            if (title is null)
                throw new ImmutableContentIntegrityException("A visible policy has no authored title at its native source.");
            values.Add(new PolicySummaryView(tenantId, programId, row.PolicyId, row.Identifier, title,
                view.Status, view.PendingStatus, view.Revision, view.CurrentVersion, view.CurrentEffectiveFrom,
                view.NextReviewDueOn, false, view.LastChangedAt));
        }
        return new Page<PolicySummaryView>(values, page.NextCursor);
    }
}
