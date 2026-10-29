using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
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
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.Snapshots.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;
namespace Bdgrz.Compliance.Features.Workforce.Projections;

/// <summary>
///     Captures the accepted roster only after both roster projections have reached their sources
///     and proves neither advanced during the read (a fenced capture), then freezes it through the reusable population snapshot primitive (R1-11d on EN-03).
/// </summary>
public sealed class WorkforceRosterSnapshotter(IPersonDirectoryReader people,
    PersonReadConsistency peopleConsistency, IWorkRelationshipDirectoryReader relationships,
    WorkRelationshipReadConsistency relationshipConsistency, PopulationSnapshotFreezer freezer)
{
    public async ValueTask<Result<SnapshotRegistration>> FreezeAsync<TRequest>(
        IRequestContext<TRequest> context, Uuid? amendsSnapshotId, string? reason,
        CancellationToken ct) where TRequest : IRequestBase
    {
        var tenantId = ((IWorkforceRequest)context.Request).TenantId;
        var peopleFence = await peopleConsistency.CaptureListFenceAsync(tenantId, ct)
            .ConfigureAwait(false);
        if (!peopleFence.IsSuccess)
            return Result<SnapshotRegistration>.Failure(peopleFence.Error);
        var relationshipsFence = await relationshipConsistency.CaptureListFenceAsync(tenantId, ct)
            .ConfigureAwait(false);
        if (!relationshipsFence.IsSuccess)
            return Result<SnapshotRegistration>.Failure(relationshipsFence.Error);

        var roster = await ReadAllAsync((cursor, token) =>
            people.ListAsync(tenantId, 200, cursor, token), ct).ConfigureAwait(false);
        var jobs = await ReadAllAsync((cursor, token) =>
            relationships.ListAsync(tenantId, 200, cursor, token), ct).ConfigureAwait(false);
        if (roster is null || jobs is null)
            return Result<SnapshotRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                $"A roster snapshot supports at most {PopulationContentIdentity.MaximumRows} rows."));
        var verifiedRoster = new List<PersonView>();
        foreach (var person in roster)
        {
            if (person.TenantId != tenantId)
                return Result<SnapshotRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The roster projection returned content outside the tenant."));
            var source = await peopleConsistency.GetAsync(tenantId, person.PersonId, person.Revision, ct).ConfigureAwait(false);
            if (!source.IsSuccess)
                return Result<SnapshotRegistration>.Failure(source.Error!);
            verifiedRoster.Add(source.Value);
        }
        // Fence: pages are read in separate transactions, so a projection that advanced after the
        // catch-up check could mix versions. Refuse rather than freeze a torn roster.
        var peopleHeld = await peopleConsistency.EnsureFenceHeldAsync(tenantId, peopleFence.Value,
            ct).ConfigureAwait(false);
        if (!peopleHeld.IsSuccess)
            return Result<SnapshotRegistration>.Failure(peopleHeld.Error);
        var relationshipsHeld = await relationshipConsistency.EnsureFenceHeldAsync(tenantId,
            relationshipsFence.Value, ct).ConfigureAwait(false);
        if (!relationshipsHeld.IsSuccess)
            return Result<SnapshotRegistration>.Failure(relationshipsHeld.Error);
        if (roster.Any(person => person.TenantId != tenantId) ||
            jobs.Any(job => job.TenantId != tenantId))
            return Result<SnapshotRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The roster projection returned content outside the tenant."));

        return await freezer.FreezeAsync(context, tenantId, WorkforceRosterSnapshotContent.Kind,
            WorkforceRosterSnapshotContent.Rows(verifiedRoster, jobs), amendsSnapshotId, reason,
            WorkforceActor.From(context), ct).ConfigureAwait(false);
    }

    static async ValueTask<List<T>?> ReadAllAsync<T>(
        Func<string?, CancellationToken, ValueTask<Page<T>>> read, CancellationToken ct)
    {
        var items = new List<T>();
        string? cursor = null;
        do
        {
            var page = await read(cursor, ct).ConfigureAwait(false);
            items.AddRange(page.Items);
            if (items.Count > PopulationContentIdentity.MaximumRows)
                return null;
            cursor = page.NextCursor;
        } while (cursor is not null);
        return items;
    }
}
