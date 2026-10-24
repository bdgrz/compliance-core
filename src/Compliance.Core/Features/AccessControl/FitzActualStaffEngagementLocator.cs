using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The internal discovery projection of the staff members actually assigned to accepted engagements. It reads the
///     acceptance and staff assignment facts of the client's engagement owners and the immutable accepted snapshot each
///     retains; the rows are source discovery only, neither membership nor professional authority.
/// </summary>
sealed class FitzActualStaffEngagementLocator(IKvClient client, IImmutableContentStore content)
    : FitzKvProjectionStore(client, "kv://bdgrz/actual-staff-engagement-locator/projection-v1",
        ActualStaffEngagementLocatorProjector.WorkloadName),
        IActualStaffEngagementLocatorProjection, IActualStaffEngagementLocatorReader
{
    CheckpointIdentity? _batchIdentity;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context, CancellationToken ct = default) =>
        BeginLocatorBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context, CancellationToken ct) =>
        BeginLocatorBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginLocatorBatchAsync(ProjectionBatchContext context, CancellationToken ct)
    {
        if (!Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenant) || tenant == Uuid.Empty ||
            context.Identity.Pattern.Area != ServiceEngagementIndependence.Area || context.Identity.Pattern.Resource is not null)
            throw new InvalidOperationException("Actual staff discovery requires its bound owning client workload.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchIdentity = context.Identity;
        return batch;
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ActualStaffEngagementLocatorProjector.WorkloadName,
            EventStreamPattern.ForPattern(tenantId.ToString(), ServiceEngagementIndependence.Area)), ct);

    public async ValueTask ApplyAsync(DomainEventRecord source, IProjectorContext context, CancellationToken ct)
    {
        if (context.Identity != _batchIdentity ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenant) || tenant == Uuid.Empty ||
            context.Identity.Pattern.Area != ServiceEngagementIndependence.Area ||
            source.Stream.Realm != tenant.ToString() || source.Stream.Area != ServiceEngagementIndependence.Area ||
            !Uuid.TryParse(source.Stream.Resource, null, out var engagementId) || engagementId == Uuid.Empty)
            throw new InvalidOperationException("Actual staff discovery requires the exact owning engagement stream.");
        if (source.Event is not (ServiceEngagementAcceptanceRecorded or ServiceEngagementStaffAssigned) ||
            source.Event is not INativeOperationFact fact)
            return;
        var realm = IndependenceContent.Realm(tenant);
        var (operationId, resultReference, revision, assignedStaff) = source.Event switch
        {
            ServiceEngagementAcceptanceRecorded recorded => (recorded.OperationId, recorded.OperationResult,
                recorded.Revision, (Uuid?)null),
            ServiceEngagementStaffAssigned assigned => (assigned.OperationId, assigned.OperationResult,
                assigned.Revision, assigned.EngagementId == engagementId
                    ? (await FirmStaffContent.ReadStaffAsync(content, assigned.AssignedStaffContent, ct)
                        .ConfigureAwait(false)).StaffMemberId
                    : Uuid.Empty),
            _ => throw new InvalidOperationException("Unreachable."),
        };
        if (operationId == Uuid.Empty || resultReference is null || fact.Operation.RootOperationId != operationId ||
            revision == 0 || source.NextCursor.Value is null || assignedStaff == Uuid.Empty)
            throw new InvalidOperationException("Actual staff discovery requires immutable valid accepted assignment provenance.");
        var view = await IndependenceContent.ReadResultAsync<ServiceEngagementAcceptanceView>(content, realm,
            resultReference, ct).ConfigureAwait(false);
        var assignments = assignedStaff is { } only
            ? view.Assignments.Where(staff => staff.StaffMemberId == only).ToArray()
            : view.Assignments.ToArray();
        var recordedAt = (source.Event as ServiceEngagementStaffAssigned)?.RecordedAt ?? view.RecordedAt;
        if (view.TenantId != tenant || view.EngagementId != engagementId || view.Status != "active" ||
            view.RecordedAt == default || assignments.Length < 1 || view.Assignments.Count > 1000 ||
            assignedStaff is null && assignments.Length != view.Assignments.Count ||
            assignments.Any(staff => staff is null || staff.StaffMemberId == Uuid.Empty || staff.UserId == Uuid.Empty ||
                staff.Practice is not ("attest" or "advisory") || staff.DirectoryStaffRevision <= 0 || !staff.IsCurrent ||
                staff.DirectoryStaffRecordedAt == default || staff.DirectoryStaffRecordedAt > staff.AssignedAt ||
                staff.AssignedAt == default || staff.AssignedAt > recordedAt) ||
            assignments.Select(staff => staff.StaffMemberId).Distinct().Count() != assignments.Length ||
            assignments.Select(staff => staff.UserId).Distinct().Count() != assignments.Length)
            throw new InvalidOperationException("Actual staff discovery requires immutable valid accepted assignment provenance.");
        var digest = IndependenceSourceDigest.Acceptance(view);
        var sourceDigest = IndependenceSourceDigest.AcceptanceSource(source.Event);
        var rows = assignments.Select(staff => new ActualStaffEngagementLocatorView(
            Uuid.CreateVersion5(operationId,
                $"actual_staff_locator_v1:{tenant}:{engagementId}:{view.Revision}:{staff.StaffMemberId}:{staff.UserId}"),
            tenant, engagementId, operationId, view.Revision, (long)revision, digest, sourceDigest,
            staff.StaffMemberId, staff.UserId, staff.Practice, staff.DirectoryStaffRevision, view.RecordedAt,
            staff.AssignedAt, source.Stream.Realm, source.Stream.Area, source.Stream.Resource,
            source.ResourceOffset, source.NextCursor.Value)).ToArray();
        // Validate every immutable predecessor before inserting any new row; batch rollback also retains its checkpoint.
        foreach (var row in rows)
            if (await ActualStaffEngagementLocatorSchema.Rows.GetAsync(Transaction, row.LocatorId, ct).ConfigureAwait(false) is { } existing && existing != row)
                throw new InvalidOperationException("An immutable actual staff locator already retains different source facts.");
        foreach (var row in rows)
            if (await ActualStaffEngagementLocatorSchema.Rows.GetAsync(Transaction, row.LocatorId, ct).ConfigureAwait(false) is null)
                await ActualStaffEngagementLocatorSchema.Rows.InsertAsync(Transaction, row, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<ActualStaffEngagementLocatorView>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ActualStaffEngagementLocatorSchema.Rows.QueryAsync(tx,
            ActualStaffEngagementLocatorSchema.ByStaff.Query().Take(Math.Clamp(limit, 1, 200)).After(cursor), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ActualStaffEngagementLocatorView>> ListForStaffAsync(Uuid tenantId,
        Uuid staffMemberId, Uuid canonicalUserId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ActualStaffEngagementLocatorSchema.Rows.QueryAsync(tx,
            ActualStaffEngagementLocatorSchema.ByStaff.Query().WithPrefix(staffMemberId.ToString(), canonicalUserId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }
}
