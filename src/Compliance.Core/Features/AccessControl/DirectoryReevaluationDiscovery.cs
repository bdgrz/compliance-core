using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Finds the accepted assignments a directory status change affects and fans out one internal reevaluation command
///     per affected acceptance pair. Discovery reads the authoritative acceptance and staff assignment facts of the
///     client's engagement owners and the immutable accepted snapshot each retains; the actual staff locator only
///     narrows the search and its rows are verified against those facts. A receipt never reactivates an acceptance.
/// </summary>
sealed class DirectoryReevaluationDiscovery(IDomainEventReader events, IAggregateReader reader,
    FirmStaffViews staffViews, IActualStaffEngagementLocatorReader locators, IImmutableContentStore content,
    IRequestBus bus)
{
    public async ValueTask ReconcileStatusAsync(Uuid tenantId, IReactorContext<FirmStaffChangeRecorded> context,
        FirmStaffMemberView changed, CancellationToken ct)
    {
        var sources = await SourcesAsync(tenantId, ct).ConfigureAwait(false);
        var checkpoint = await locators.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        var coveredPosition = checkpoint.Cursor == EventCursor.Start ? -1 :
            sources.FindIndex(record => record.NextCursor == checkpoint.Cursor);
        var lastAcceptance = sources.FindLastIndex(IsAcceptanceSource);
        if (lastAcceptance > coveredPosition)
            throw Pending("The actual assignment locator has not covered authoritative acceptance sources.");
        var accepted = new List<AcceptedSource>();
        foreach (var record in sources.Where(IsAcceptanceSource))
            accepted.Add(await ReadAcceptedAsync(tenantId, record, ct).ConfigureAwait(false));
        foreach (var original in accepted)
            if (!await RetainsAsync(tenantId, original, ct).ConfigureAwait(false))
                throw new InvalidOperationException("Assignment discovery requires exact retained acceptance source.");
        var expected = accepted.Where(source => source.Assignees.Any(staff =>
                staff.StaffMemberId == changed.StaffMemberId && staff.UserId == changed.UserId &&
                staff.Practice == changed.Practice))
            .Select(source => LocatorId(source, changed.StaffMemberId, changed.UserId)).ToHashSet();
        var seen = new HashSet<Uuid>();
        string? cursor = null;
        do
        {
            var page = await locators.ListForStaffAsync(tenantId, changed.StaffMemberId,
                changed.UserId, 200, cursor, ct).ConfigureAwait(false);
            foreach (var row in page.Items)
            {
                var source = accepted.SingleOrDefault(item => item.Record.ResourceOffset == row.SourceResourceOffset &&
                    item.Record.Stream.Realm == row.SourceRealm && item.Record.Stream.Area == row.SourceArea &&
                    item.Record.Stream.Resource == row.SourceResource);
                if (source is null || row.TenantId != tenantId || row.AcceptanceRequestId != source.OperationId ||
                    row.EngagementId != source.View.EngagementId ||
                    row.AcceptanceSourceSequence != (long)source.Revision ||
                    row.AcceptanceRevision != source.View.Revision ||
                    row.AcceptanceSha256 != IndependenceSourceDigest.Acceptance(source.View) ||
                    row.SourcePayloadSha256 != IndependenceSourceDigest.AcceptanceSource(source.Record.Event) ||
                    !source.Assignees.Any(staff => staff.StaffMemberId == row.StaffMemberId &&
                        staff.UserId == row.UserId && staff.Practice == row.Practice &&
                        staff.DirectoryStaffRevision == row.DirectoryStaffRevision &&
                        staff.AssignedAt == row.AssignedAt) || row.StaffMemberId != changed.StaffMemberId ||
                    row.UserId != changed.UserId || row.Practice != changed.Practice)
                    throw new InvalidOperationException("An assignment locator must match its exact authoritative acceptance.");
                if (row.LocatorId != LocatorId(source, row.StaffMemberId, row.UserId) || !expected.Contains(row.LocatorId))
                    throw new InvalidOperationException("An assignment locator must preserve its exact immutable source identity.");
                if (!seen.Add(row.LocatorId))
                    throw Pending("Assignment discovery returned a duplicate immutable source row.");
                if (row.DirectoryStaffRevision < changed.Revision)
                    await bus.SendReactionAsync(Command(tenantId, context.Source, source, row.StaffMemberId, row.UserId),
                        context, ct).ConfigureAwait(false);
            }
            cursor = page.NextCursor;
        } while (cursor is not null);
        if (!expected.SetEquals(seen))
            throw Pending("Assignment discovery has not returned every authoritative selected staff source.");
        var after = (await SourcesAsync(tenantId, ct).ConfigureAwait(false)).Where(IsAcceptanceSource).ToArray();
        if (!accepted.Select(item => SourceIdentity(item.Record)).SequenceEqual(after.Select(SourceIdentity)))
            throw Pending("Authoritative acceptance sources changed while assignment discovery was paged.");
    }

    public async ValueTask ReconcileAcceptanceAsync(IReactorContext context, DomainEvent trigger, CancellationToken ct)
    {
        if (trigger is not (ServiceEngagementAcceptanceRecorded or ServiceEngagementStaffAssigned) ||
            !Uuid.TryParse(context.Source.Stream.Realm, null, out var tenantId))
            throw new InvalidOperationException("Reconciliation requires an acceptance source of its owning client.");
        var source = await ReadAcceptedAsync(tenantId, context.Source, ct).ConfigureAwait(false);
        if (!await RetainsAsync(tenantId, source, ct).ConfigureAwait(false))
            throw new InvalidOperationException("Reconciliation requires the exact retained original acceptance.");
        foreach (var staffMemberId in source.Assignees.Select(staff => staff.StaffMemberId).Distinct())
        {
            var record = await reader.HydrateAsync(new FirmStaffRecord(staffMemberId), ct).ConfigureAwait(false);
            var history = await FirmStaffHistory.ReadAsync(events, record, ct).ConfigureAwait(false);
            await foreach (var directory in events.ReadAsync(record.Stream, 0, ct).WithCancellation(ct).ConfigureAwait(false))
            {
                if (directory.ResourceOffset >= record.CommittedStreamPosition)
                    break;
                if (directory.Stream != record.Stream)
                    throw new InvalidOperationException("A staff cause must originate on its authoritative record stream.");
                if (directory.Event is not FirmStaffChangeRecorded { ChangeKind: "status" } change)
                    continue;
                if (!history.Matches(change))
                    throw Pending("Staff source history changed while accepted assignments were reconciled.");
                var changed = await staffViews.ReadAsync(change, ct).ConfigureAwait(false);
                foreach (var staff in source.Assignees.Where(staff =>
                             staff.StaffMemberId == changed.StaffMemberId && staff.UserId == changed.UserId &&
                             staff.Practice == changed.Practice && staff.DirectoryStaffRevision < changed.Revision))
                    await bus.SendReactionAsync(Command(tenantId, directory, source, staff.StaffMemberId, staff.UserId),
                        context, ct).ConfigureAwait(false);
            }
        }
    }

    async ValueTask<List<DomainEventRecord>> SourcesAsync(Uuid tenantId, CancellationToken ct)
    {
        var records = new List<DomainEventRecord>();
        await foreach (var source in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           ServiceEngagementIndependence.Area), EventCursor.Start, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            if (source.Stream.Realm != tenantId.ToString() || source.Stream.Area != ServiceEngagementIndependence.Area)
                throw new InvalidOperationException("Accepted source coverage requires its exact owning engagement streams.");
            records.Add(source);
        }

        return records;
    }

    async ValueTask<AcceptedSource> ReadAcceptedAsync(Uuid tenantId, DomainEventRecord record, CancellationToken ct)
    {
        var realm = IndependenceContent.Realm(tenantId);
        var (operationId, result, revision, assignedStaff) = record.Event switch
        {
            ServiceEngagementAcceptanceRecorded recorded => (recorded.OperationId, recorded.OperationResult,
                recorded.Revision, (Uuid?)null),
            ServiceEngagementStaffAssigned assigned => (assigned.OperationId, assigned.OperationResult,
                assigned.Revision, (Uuid?)(await FirmStaffContent.ReadStaffAsync(content, assigned.AssignedStaffContent,
                    ct).ConfigureAwait(false)).StaffMemberId),
            _ => throw new InvalidOperationException("The fact is not an acceptance source."),
        };
        if (result is null)
            throw new InvalidOperationException("Assignment discovery requires an acceptance result.");
        var view = await IndependenceContent.ReadResultAsync<ServiceEngagementAcceptanceView>(content, realm, result, ct)
            .ConfigureAwait(false);
        var assignees = assignedStaff is { } only
            ? view.Assignments.Where(staff => staff.StaffMemberId == only).ToArray()
            : view.Assignments.ToArray();
        return new AcceptedSource(record, view, operationId, revision, assignees);
    }

    async ValueTask<bool> RetainsAsync(Uuid tenantId, AcceptedSource source, CancellationToken ct)
    {
        if (!Uuid.TryParse(source.Record.Stream.Resource, null, out var engagementId))
            return false;
        var owner = await reader.HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId), ct)
            .ConfigureAwait(false);
        var snapshot = await ServiceEngagementHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        return owner.Stream == source.Record.Stream &&
            snapshot.OperationOf(source.OperationId) is { Completed: { } completed } &&
            completed.Metadata.EventId == source.Record.Event.Metadata.EventId;
    }

    static bool IsAcceptanceSource(DomainEventRecord record) =>
        !record.Event.Metadata.IsAudit && record.Event is ServiceEngagementAcceptanceRecorded or ServiceEngagementStaffAssigned;

    static Uuid LocatorId(AcceptedSource source, Uuid staffMemberId, Uuid userId) =>
        Uuid.CreateVersion5(source.OperationId,
            $"actual_staff_locator_v1:{source.View.TenantId}:{source.View.EngagementId}:{source.View.Revision}:{staffMemberId}:{userId}");

    static (Uuid EventId, ulong Offset, string Digest) SourceIdentity(DomainEventRecord source) =>
        (source.Event.Metadata.EventId, source.ResourceOffset, IndependenceSourceDigest.AcceptanceSource(source.Event));

    static RecordDirectoryIndependenceReevaluation Command(Uuid tenantId, DomainEventRecord directory,
        AcceptedSource acceptance, Uuid staffMemberId, Uuid userId) => new(
            RecordDirectoryIndependenceReevaluation.CommandIdOf(directory.Event.Metadata.EventId,
                acceptance.Record.Event.Metadata.EventId, staffMemberId, userId), tenantId,
            directory.ResourceOffset, directory.Event.Metadata.EventId,
            IndependenceSourceDigest.DirectoryEvent((FirmStaffChangeRecorded)directory.Event),
            acceptance.Record.ResourceOffset, acceptance.Record.Event.Metadata.EventId, acceptance.OperationId,
            IndependenceSourceDigest.AcceptanceSource(acceptance.Record.Event), staffMemberId, userId,
            acceptance.View.EngagementId, acceptance.View.CompleteServiceHistory.Count);

    static ReactionCommandFailedException Pending(string reason) => new(new RequestError(RequestErrorKind.Conflict,
        reason, isTransient: true));

    sealed record AcceptedSource(DomainEventRecord Record, ServiceEngagementAcceptanceView View, Uuid OperationId,
        ulong Revision, IReadOnlyList<EngagementActualAssignmentView> Assignees);
}
