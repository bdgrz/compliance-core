using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Records the denial-only receipt that one accepted staff member's directory record changed after the acceptance.
///     The command targets exactly one engagement acceptance pair and is verified against the authoritative directory
///     decision and the authoritative accepted fact it names; a receipt never reactivates an old acceptance and
///     supplies no partner evidence. The receipt is a root fact of the engagement owner whose identity derives from
///     its sources, so an exact redelivery is the same operation.
/// </summary>
sealed class RecordDirectoryIndependenceReevaluationHandler(NativeOperationExecutor operations,
    IDomainEventReader events, IAggregateReader reader, FirmStaffViews staffViews, IImmutableContentStore content,
    ITenantActivity tenants, TimeProvider clock)
    : IRequestHandler<RecordDirectoryIndependenceReevaluation>
{
    const string CommandKind = "RecordDirectoryIndependenceReevaluation";
    const string InitialEpisode = "initial";
    const string ProcessName = "directory-independence-reevaluation";

    public async ValueTask<Result> HandleAsync(IRequestContext<RecordDirectoryIndependenceReevaluation> context,
        CancellationToken ct)
    {
        var process = RecordDirectoryIndependenceReevaluationAuthorizer.Process(context);
        if (process is null)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the named internal reconciliation workers may record directory receipts."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.StaffMemberId == Uuid.Empty ||
            request.UserId == Uuid.Empty || request.EngagementId == Uuid.Empty ||
            request.CommandId != RecordDirectoryIndependenceReevaluation.CommandIdOf(request.DirectoryEventId,
                request.AcceptanceEventId, request.StaffMemberId, request.UserId))
            return RefuseSource();
        var directory = await reader.HydrateAsync(new FirmStaffRecord(request.StaffMemberId), ct).ConfigureAwait(false);
        var engagement = await reader.HydrateAsync(new ServiceEngagementIndependence(request.TenantId,
            request.EngagementId, request.CommandId), ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var actor = ActorReference.ForSystemProcess(ProcessName, ProcessName);
        var provenance = new OperationProvenance(request.CommandId, request.CommandId, null, CommandKind, actor,
            engagement.Stream, InitialEpisode, null, AuthoredIntentDigest.Compute(request), now, engagement.Stream,
            engagement.Stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        // An exact redelivery resolves from the owner's accepted facts before any source is re-read.
        switch (engagement.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return Result.Success;
            case NativeAcceptanceDecision.Conflict:
                return Result.Failure(AcceptedAppendResult.ConflictError);
        }

        if (request.DirectoryResourceOffset >= directory.CommittedStreamPosition ||
            request.AcceptanceResourceOffset >= engagement.CommittedStreamPosition)
            return RefuseSource();
        var directoryRecord = await ReadRecordAsync(directory.Stream, request.DirectoryResourceOffset, ct)
            .ConfigureAwait(false);
        var acceptedRecord = await ReadRecordAsync(engagement.Stream, request.AcceptanceResourceOffset, ct)
            .ConfigureAwait(false);
        if (directoryRecord?.Event is not FirmStaffChangeRecorded { ChangeKind: "status" } change ||
            acceptedRecord?.Event is not (ServiceEngagementAcceptanceRecorded or ServiceEngagementStaffAssigned) ||
            acceptedRecord.Event is not INativeOperationFact original ||
            OriginalResult(acceptedRecord.Event) is not { } originalResult ||
            directoryRecord.Event.Metadata.EventId != request.DirectoryEventId ||
            acceptedRecord.Event.Metadata.EventId != request.AcceptanceEventId ||
            original.Operation.RootOperationId != request.AcceptanceRequestId ||
            IndependenceSourceDigest.DirectoryEvent(change) != request.DirectoryPayloadSha256 ||
            IndependenceSourceDigest.AcceptanceSource(acceptedRecord.Event) != request.AcceptancePayloadSha256 ||
            context.CausationId != (process == "reactor:FirmStaffStatusReevaluationV1"
                ? request.DirectoryEventId : request.AcceptanceEventId))
            return RefuseSource();
        if (!directory.MatchesRetainedChange(change) || engagement.OperationOf(request.AcceptanceRequestId) is not
            { Completed: { } completed } || completed.Metadata.EventId != request.AcceptanceEventId)
            return RefuseSource();
        FirmStaffMemberView staff;
        ServiceEngagementAcceptanceView accepted;
        ServiceEngagementAcceptanceView observed;
        var realm = IndependenceContent.Realm(request.TenantId);
        try
        {
            staff = await staffViews.ReadAsync(change, ct).ConfigureAwait(false);
            accepted = await IndependenceContent.ReadResultAsync<ServiceEngagementAcceptanceView>(content, realm,
                originalResult, ct).ConfigureAwait(false);
            observed = engagement.AcceptanceResult is { } current
                ? await IndependenceContent.ReadResultAsync<ServiceEngagementAcceptanceView>(content, realm, current, ct)
                    .ConfigureAwait(false)
                : accepted;
        }
        catch (ImmutableContentIntegrityException)
        {
            return RefuseSource();
        }

        if (staff.StaffMemberId != request.StaffMemberId || staff.UserId != request.UserId ||
            accepted.CompleteServiceHistory.Count != request.AcceptedServiceEpoch ||
            accepted.EngagementId != request.EngagementId || accepted.TenantId != request.TenantId ||
            !accepted.Assignments.Any(assignment => assignment.StaffMemberId == staff.StaffMemberId &&
                assignment.UserId == staff.UserId && assignment.Practice == staff.Practice &&
                assignment.DirectoryStaffRevision < staff.Revision && assignment.DirectoryStaffRecordedAt <= staff.RecordedAt))
            return RefuseSource();
        if (!await tenants.IsActiveAsync(request.TenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The owning client is inactive; reconciliation remains pending.", isTransient: true));

        var source = new DirectoryStatusSourceView(request.DirectoryEventId, change.OperationId,
            (long)change.Revision, directoryRecord.ResourceOffset, request.DirectoryPayloadSha256,
            staff.StaffMemberId, staff.UserId, staff.Practice, staff.Revision, staff.IsActive, staff.RecordedAt);
        var unit = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(request.CommandId, UnitResult.Schema),
            UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
        var response = new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType,
            UnitResult.Schema, new StandardResponseBody(unit));
        var receipt = new DirectoryIndependenceReevaluationView(
            Uuid.CreateVersion5(source.RequestId,
                $"directory_reevaluation_v1:{request.TenantId}:{request.AcceptanceRequestId}:{request.EngagementId}:{staff.StaffMemberId}:{staff.UserId}"),
            request.TenantId, source, IndependenceSourceDigest.DirectorySource(source), request.AcceptanceRequestId,
            request.AcceptancePayloadSha256, accepted, IndependenceSourceDigest.Acceptance(accepted), observed,
            IndependenceSourceDigest.Acceptance(observed), "review_required", true, actor, now);
        var receiptContent = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(request.CommandId,
            "DirectoryIndependenceReevaluated.ReevaluationContent.v1"),
            "DirectoryIndependenceReevaluated.ReevaluationContent.v1", receipt, ct).ConfigureAwait(false);
        var result = await AdministratorTransitionEffects.UnitResultAsync(content, realm, request.CommandId, ct)
            .ConfigureAwait(false);
        var outcome = await operations.ExecuteAsync(new NativeOperation<ServiceEngagementIndependence>(binding,
            () => new ServiceEngagementIndependence(request.TenantId, request.EngagementId, request.CommandId),
            owner => owner.Acceptance, owner => Prepare(owner, response),
            (ServiceEngagementIndependence owner, ImmutableContentRef frozen) =>
                owner.RecordDirectoryReevaluation(provenance, receiptContent, frozen, result)), context, ct)
            .ConfigureAwait(false);
        if (!outcome.IsSuccess)
            return Result.Failure(outcome.Error!);
        return outcome.Value switch
        {
            AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed => Result.Success,
            _ => Result.Failure(AcceptedAppendResult.ConflictError),
        };
    }

    async ValueTask<DomainEventRecord?> ReadRecordAsync(EventStreamAddress stream, ulong offset, CancellationToken ct)
    {
        await foreach (var record in events.ReadAsync(stream, offset, ct).WithCancellation(ct).ConfigureAwait(false))
            return record.Stream == stream && record.ResourceOffset == offset ? record : null;
        return null;
    }

    static ImmutableContentRef? OriginalResult(DomainEvent source) => source switch
    {
        ServiceEngagementAcceptanceRecorded recorded => recorded.OperationResult,
        ServiceEngagementStaffAssigned assigned => assigned.OperationResult,
        _ => null,
    };

    static Result<FirstResponseDescriptor> Prepare(ServiceEngagementIndependence owner, FirstResponseDescriptor response)
    {
        var prepared = owner.PrepareDirectoryReevaluation();
        return prepared.IsSuccess
            ? Result<FirstResponseDescriptor>.Success(response)
            : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
    }

    static Result RefuseSource() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The directory receipt requires its exact authoritative cause and original client acceptance."));
}
