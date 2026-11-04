using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Projections;
namespace Bdgrz.Compliance.Features.Workforce.Readers;

sealed class FitzWorkRelationshipDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/work-relationship-directory-v1/projection",
            "WorkRelationshipDirectoryV1"),
        IWorkRelationshipDirectoryReader, IWorkRelationshipDirectoryProjection
{
    public const string ManualSource = "manual";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("WorkRelationshipDirectoryV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "work-relationships")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case WorkRelationshipRecorded recorded:
                await WorkRelationshipDirectorySchema.Relationships.InsertAsync(Transaction,
                    new WorkRelationshipView(recorded.TenantId, recorded.RelationshipId, 1,
                        recorded.PersonId, recorded.SourceWorkerId, recorded.Terms.WorkerType,
                        recorded.Terms.LifecycleStatus, recorded.Terms.StartDate,
                        recorded.Terms.EndDate, recorded.Terms.Department,
                        recorded.Terms.ManagerPersonId, recorded.Terms.SponsorPersonId, false,
                        recorded.Source?.SourceKind ?? ManualSource, recorded.Actor, recorded.ChangedAt,
                        recorded.Terms.EmploymentStatusReason, Source: recorded.Source), ct).ConfigureAwait(false);
                break;
            case WorkRelationshipRevised revised:
                var current = await WorkRelationshipDirectorySchema.Relationships.GetAsync(
                    Transaction, revised.RelationshipId, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != revised.TenantId ||
                    current.Revision + 1 != revised.Revision)
                    throw new InvalidOperationException(
                        "A work relationship revision cannot project before its predecessor.");
                await WorkRelationshipDirectorySchema.Relationships.ReplaceAsync(Transaction,
                    current, current with
                    {
                        Revision = revised.Revision,
                        WorkerType = revised.Terms.WorkerType,
                        LifecycleStatus = revised.Terms.LifecycleStatus,
                        StartDate = revised.Terms.StartDate,
                        EndDate = revised.Terms.EndDate,
                        Department = revised.Terms.Department,
                        ManagerPersonId = revised.Terms.ManagerPersonId,
                        SponsorPersonId = revised.Terms.SponsorPersonId,
                        EmploymentStatusReason = revised.Terms.EmploymentStatusReason,
                        LastChangedBy = revised.Actor,
                        LastChangedAt = revised.ChangedAt,
                    }, ct).ConfigureAwait(false);
                break;
            case WorkRelationshipStarted started:
                await ReplaceLifecycleAsync(started.TenantId, started.RelationshipId, started.WorkerIdentifier,
                    started.Revision, started.Actor, started.ChangedAt,
                    view => view with { LifecycleStatus = "active", StartDate = started.StartDate }, ct)
                    .ConfigureAwait(false);
                break;
            case WorkRelationshipLeaveStarted leave:
                await ReplaceLifecycleAsync(leave.TenantId, leave.RelationshipId, leave.WorkerIdentifier,
                    leave.Revision, leave.Actor, leave.ChangedAt,
                    view => view with { LifecycleStatus = "on_leave" }, ct).ConfigureAwait(false);
                break;
            case WorkRelationshipLeaveEnded returned:
                await ReplaceLifecycleAsync(returned.TenantId, returned.RelationshipId, returned.WorkerIdentifier,
                    returned.Revision, returned.Actor, returned.ChangedAt,
                    view => view with { LifecycleStatus = "active" }, ct).ConfigureAwait(false);
                break;
            case WorkRelationshipEnded ended:
                await ReplaceLifecycleAsync(ended.TenantId, ended.RelationshipId, ended.WorkerIdentifier,
                    ended.Revision, ended.Actor, ended.ChangedAt,
                    view => view with
                    {
                        LifecycleStatus = "ended",
                        EndDate = ended.EndDate,
                        EmploymentStatusReason = ended.Reason,
                    }, ct).ConfigureAwait(false);
                break;
            case WorkRelationshipManagerChanged manager:
                await ReplaceLifecycleAsync(manager.TenantId, manager.RelationshipId, manager.WorkerIdentifier,
                    manager.Revision, manager.Actor, manager.ChangedAt,
                    view => view with { ManagerPersonId = manager.ManagerPersonId }, ct).ConfigureAwait(false);
                break;
            case WorkRelationshipDepartmentChanged department:
                await ReplaceLifecycleAsync(department.TenantId, department.RelationshipId, department.WorkerIdentifier,
                    department.Revision, department.Actor, department.ChangedAt,
                    view => view with { Department = department.Department }, ct).ConfigureAwait(false);
                break;
            case WorkRelationshipSponsorChanged sponsor:
                await ReplaceLifecycleAsync(sponsor.TenantId, sponsor.RelationshipId, sponsor.WorkerIdentifier,
                    sponsor.Revision, sponsor.Actor, sponsor.ChangedAt,
                    view => view with { SponsorPersonId = sponsor.SponsorPersonId }, ct).ConfigureAwait(false);
                break;
            case WorkRelationshipDatesCorrected dates:
                await ReplaceLifecycleAsync(dates.TenantId, dates.RelationshipId, dates.WorkerIdentifier,
                    dates.Revision, dates.Actor, dates.ChangedAt,
                    view => view with { StartDate = dates.StartDate, EndDate = dates.EndDate }, ct).ConfigureAwait(false);
                break;
        }
    }

    async ValueTask ReplaceLifecycleAsync(Uuid tenantId, Uuid relationshipId, string workerIdentifier,
        long revision, ActorReference actor, DateTimeOffset changedAt,
        Func<WorkRelationshipView, WorkRelationshipView> change, CancellationToken ct)
    {
        var current = await WorkRelationshipDirectorySchema.Relationships.GetAsync(
            Transaction, relationshipId, ct).ConfigureAwait(false);
        if (current is null || current.TenantId != tenantId || current.SourceWorkerId != workerIdentifier ||
            current.Revision + 1 != revision)
            throw new InvalidOperationException(
                "A named work relationship fact cannot project before its matching predecessor.");
        await WorkRelationshipDirectorySchema.Relationships.ReplaceAsync(Transaction, current,
            change(current) with { Revision = revision, LastChangedBy = actor, LastChangedAt = changedAt }, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<WorkRelationshipView?> GetAsync(Uuid tenantId, Uuid relationshipId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await WorkRelationshipDirectorySchema.Relationships.GetAsync(tx, relationshipId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<WorkRelationshipView>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await WorkRelationshipDirectorySchema.Relationships.QueryAsync(tx,
            WorkRelationshipDirectorySchema.BySourceWorkerId.Query().Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }
}
