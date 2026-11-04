using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Projections;

namespace Bdgrz.Compliance.Features.Workforce.Readers;

/// <summary>
///     Observes joiners, movers, and leavers by comparing each accepted roster version with the
///     previous one (M0-D06). Observations are compliance work only: nothing here grants or revokes
///     platform access. Manager changes are restricted and never produce list-visible observations.
/// </summary>
sealed class CassieWorkforceObservationDirectory(CassieProjectionDatabase database)
    : CassieWorkforceRepository(database, "WorkforceObservationsV1", "work-relationships"),
        IWorkforceObservationDirectoryReader, IWorkforceObservationProjection
{
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("WorkforceObservationsV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "work-relationships")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case WorkRelationshipRecorded recorded:
                var initial = new RosterRelationshipSnapshot(recorded.TenantId,
                    recorded.RelationshipId, recorded.PersonId, recorded.SourceWorkerId, 1,
                    recorded.Terms);
                await InsertSnapshotAsync(initial, ct)
                    .ConfigureAwait(false);
                var ended = recorded.Terms.LifecycleStatus == "ended";
                await ObserveAsync(initial, ended ? "leaver" : "joiner",
                    ended ? recorded.Terms.EndDate!.Value : recorded.Terms.StartDate, [],
                    recorded.Actor, recorded.ChangedAt, ct).ConfigureAwait(false);
                break;
            case WorkRelationshipRevised revised:
                var current = await FindSnapshotAsync(revised.RelationshipId, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != revised.TenantId ||
                    current.Revision + 1 != revised.Revision)
                    throw new InvalidOperationException(
                        "A work relationship revision cannot be observed before its predecessor.");
                var next = current with { Revision = revised.Revision, Terms = revised.Terms };
                await ReplaceSnapshotAsync(next, ct).ConfigureAwait(false);
                var wasEnded = current.Terms.LifecycleStatus == "ended";
                var isEnded = revised.Terms.LifecycleStatus == "ended";
                if (!wasEnded && isEnded)
                    await ObserveAsync(next, "leaver", revised.Terms.EndDate!.Value, [],
                        revised.Actor, revised.ChangedAt, ct).ConfigureAwait(false);
                else if (wasEnded && !isEnded)
                    await ObserveAsync(next, "joiner", revised.Terms.StartDate, [],
                        revised.Actor, revised.ChangedAt, ct).ConfigureAwait(false);
                else if (!isEnded && MovedFields(current.Terms, revised.Terms) is { Count: > 0 } moved)
                    await ObserveAsync(next, "mover", DateOnly.FromDateTime(
                            revised.ChangedAt.UtcDateTime), moved, revised.Actor,
                        revised.ChangedAt, ct).ConfigureAwait(false);
                break;
            case WorkRelationshipStarted started:
                await ApplyLifecycleAsync(started.TenantId, started.RelationshipId, started.WorkerIdentifier,
                    started.Revision, terms => terms with { LifecycleStatus = "active", StartDate = started.StartDate },
                    null, started.StartDate, started.Actor, started.ChangedAt, [], ct).ConfigureAwait(false);
                break;
            case WorkRelationshipLeaveStarted leave:
                await ApplyLifecycleAsync(leave.TenantId, leave.RelationshipId, leave.WorkerIdentifier,
                    leave.Revision, terms => terms with { LifecycleStatus = "on_leave" },
                    null, leave.FromDate, leave.Actor, leave.ChangedAt, [], ct).ConfigureAwait(false);
                break;
            case WorkRelationshipLeaveEnded returned:
                await ApplyLifecycleAsync(returned.TenantId, returned.RelationshipId, returned.WorkerIdentifier,
                    returned.Revision, terms => terms with { LifecycleStatus = "active" },
                    null, returned.ReturnDate, returned.Actor, returned.ChangedAt, [], ct).ConfigureAwait(false);
                break;
            case WorkRelationshipEnded ending:
                await ApplyLifecycleAsync(ending.TenantId, ending.RelationshipId, ending.WorkerIdentifier,
                    ending.Revision, terms => terms with
                    {
                        LifecycleStatus = "ended",
                        EndDate = ending.EndDate,
                        EmploymentStatusReason = ending.Reason,
                    }, "leaver", ending.EndDate, ending.Actor, ending.ChangedAt, [], ct).ConfigureAwait(false);
                break;
            case WorkRelationshipManagerChanged manager:
                await ApplyLifecycleAsync(manager.TenantId, manager.RelationshipId, manager.WorkerIdentifier,
                    manager.Revision, terms => terms with { ManagerPersonId = manager.ManagerPersonId },
                    null, DateOnly.FromDateTime(manager.ChangedAt.UtcDateTime), manager.Actor, manager.ChangedAt,
                    [], ct).ConfigureAwait(false);
                break;
            case WorkRelationshipDepartmentChanged department:
                await ApplyLifecycleAsync(department.TenantId, department.RelationshipId, department.WorkerIdentifier,
                    department.Revision, terms => terms with { Department = department.Department },
                    "mover", DateOnly.FromDateTime(department.ChangedAt.UtcDateTime), department.Actor, department.ChangedAt,
                    ["department"], ct).ConfigureAwait(false);
                break;
            case WorkRelationshipSponsorChanged sponsor:
                await ApplyLifecycleAsync(sponsor.TenantId, sponsor.RelationshipId, sponsor.WorkerIdentifier,
                    sponsor.Revision, terms => terms with { SponsorPersonId = sponsor.SponsorPersonId },
                    "mover", DateOnly.FromDateTime(sponsor.ChangedAt.UtcDateTime), sponsor.Actor, sponsor.ChangedAt,
                    ["sponsor"], ct).ConfigureAwait(false);
                break;
            case WorkRelationshipDatesCorrected dates:
                await ApplyLifecycleAsync(dates.TenantId, dates.RelationshipId, dates.WorkerIdentifier,
                    dates.Revision, terms => terms with { StartDate = dates.StartDate, EndDate = dates.EndDate },
                    null, DateOnly.FromDateTime(dates.ChangedAt.UtcDateTime), dates.Actor, dates.ChangedAt,
                    [], ct).ConfigureAwait(false);
                break;
        }
    }

    async ValueTask ApplyLifecycleAsync(Uuid tenantId, Uuid relationshipId, string workerIdentifier,
        long revision, Func<WorkRelationshipTerms, WorkRelationshipTerms> change,
        string? observationKind, DateOnly effectiveDate, ActorReference actor, DateTimeOffset changedAt,
        IReadOnlyList<string> changedFields, CancellationToken ct)
    {
        var current = await FindSnapshotAsync(relationshipId, ct)
            .ConfigureAwait(false);
        if (current is null || current.TenantId != tenantId || current.SourceWorkerId != workerIdentifier ||
            current.Revision + 1 != revision)
            throw new InvalidOperationException(
                "A named work relationship fact cannot be observed before its matching predecessor.");
        var next = current with { Revision = revision, Terms = change(current.Terms) };
        await ReplaceSnapshotAsync(next, ct)
            .ConfigureAwait(false);
        if (observationKind is not null)
            await ObserveAsync(next, observationKind, effectiveDate, changedFields, actor, changedAt, ct).ConfigureAwait(false);
    }

    public ValueTask<Page<WorkforceObservationView>> ListAsync(Uuid tenantId, string? kind,
        int limit, string? cursor, CancellationToken ct = default) =>
        kind is null
            ? ReadPageAsync("workforce_observations", tenantId, ComplianceCoreJsonContext.Default.WorkforceObservationView,
                limit, cursor, "all", ct)
            : ReadPageAsync("workforce_observations", tenantId, ComplianceCoreJsonContext.Default.WorkforceObservationView,
                limit, cursor, $"kind:{kind.Length}:{kind}", ct, filter: " AND kind = @kind", parameters: [("kind", kind)]);

    public async ValueTask<WorkforceObservationView?> GetAsync(Uuid tenantId, Uuid observationId,
        CancellationToken ct = default)
    {
        var view = await ReadRowAsync("workforce_observations", tenantId, observationId,
            ComplianceCoreJsonContext.Default.WorkforceObservationView, ct).ConfigureAwait(false);
        return view?.TenantId == tenantId ? view : null;
    }

    public async ValueTask<bool> HasActiveInternalRelationshipAsync(Uuid tenantId, Uuid personId,
        CancellationToken ct = default)
    {
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand("SELECT model_id FROM workforce_relationship_snapshots WHERE projection_key = @scope AND person_id = @person " +
            "AND lifecycle_status = 'active' AND (worker_type = 'employee' OR worker_type = 'contractor') LIMIT 1");
        Bind(command, ("scope", LiveProjectionKey(tenantId)), ("person", personId.ToString()));
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not null;
    }

    public async ValueTask<IReadOnlyList<string>> ListRelationshipStatusesAsync(Uuid tenantId,
        Uuid personId, CancellationToken ct = default)
    {
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand("SELECT lifecycle_status FROM workforce_relationship_snapshots WHERE projection_key = @scope AND person_id = @person ORDER BY model_id");
        Bind(command, ("scope", LiveProjectionKey(tenantId)), ("person", personId.ToString()));
        var statuses = new List<string>();
        await using var rows = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rows.ReadAsync(ct).ConfigureAwait(false))
            statuses.Add(rows.GetString(0));
        return statuses;
    }

    static List<string> MovedFields(WorkRelationshipTerms before, WorkRelationshipTerms after)
    {
        var fields = new List<string>();
        if (before.WorkerType != after.WorkerType)
            fields.Add("worker_type");
        if (before.Department != after.Department)
            fields.Add("department");
        if (before.SponsorPersonId != after.SponsorPersonId)
            fields.Add("sponsor");
        return fields;
    }

    ValueTask ObserveAsync(RosterRelationshipSnapshot relationship, string kind,
        DateOnly effectiveDate, IReadOnlyList<string> changedFields, ActorReference actor,
        DateTimeOffset observedAt, CancellationToken ct) =>
        InsertObservationAsync(
            new WorkforceObservationView(relationship.TenantId,
                Uuid.CreateVersion5(relationship.RelationshipId,
                    $"observation:{relationship.Revision}"), kind, "open",
                relationship.RelationshipId, relationship.PersonId, relationship.SourceWorkerId,
                relationship.Revision, effectiveDate, changedFields, actor, observedAt), ct);

    ValueTask<RosterRelationshipSnapshot?> FindSnapshotAsync(Uuid id, CancellationToken ct) =>
        FindCurrentAsync("workforce_relationship_snapshots", id, ComplianceCoreJsonContext.Default.RosterRelationshipSnapshot, ct);

    ValueTask InsertSnapshotAsync(RosterRelationshipSnapshot row, CancellationToken ct) =>
        InsertRowAsync("workforce_relationship_snapshots", row.TenantId, row.RelationshipId, row,
            ComplianceCoreJsonContext.Default.RosterRelationshipSnapshot, ct,
            ("person_id", row.PersonId.ToString()), ("worker_type", row.Terms.WorkerType), ("lifecycle_status", row.Terms.LifecycleStatus));

    ValueTask ReplaceSnapshotAsync(RosterRelationshipSnapshot row, CancellationToken ct) =>
        ReplaceRowAsync("workforce_relationship_snapshots", row.TenantId, row.RelationshipId, row,
            ComplianceCoreJsonContext.Default.RosterRelationshipSnapshot, ct,
            ("person_id", row.PersonId.ToString()), ("worker_type", row.Terms.WorkerType), ("lifecycle_status", row.Terms.LifecycleStatus));

    ValueTask InsertObservationAsync(WorkforceObservationView row, CancellationToken ct) =>
        InsertRowAsync("workforce_observations", row.TenantId, row.ObservationId, row,
            ComplianceCoreJsonContext.Default.WorkforceObservationView, ct,
            ("sort_key", row.ObservedAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture)), ("kind", row.Kind));
}
