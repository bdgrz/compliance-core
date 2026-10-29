using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

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
        var stream = new ServiceEngagementIndependence(request.TenantId, request.EngagementId).Stream;
        var now = clock.GetUtcNow();
        var actor = ActorReference.ForSystemProcess(ProcessName, ProcessName);
        var provenance = new OperationProvenance(request.CommandId, request.CommandId, null, CommandKind, actor,
            stream, InitialEpisode, null, AuthoredIntentDigest.Compute(request), now, stream,
            stream, FirstOwner: stream);
        var binding = AcceptedOperationBinding.Of(provenance);
        ImmutableContentRef? receiptContent = null;
        ImmutableContentRef? result = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<ServiceEngagementIndependence>(binding,
            () => new ServiceEngagementIndependence(request.TenantId, request.EngagementId, request.CommandId, request.AcceptanceRequestId),
            owner => owner.Acceptance, async (engagement, token) =>
            {
                if (request.AcceptanceResourceOffset >= engagement.CommittedStreamPosition)
                    return Result<FirstResponseDescriptor>.Failure(RefuseSource().Error!);
                var guard = engagement.PrepareDirectoryReevaluation();
                if (!guard.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(guard.Error!);
                var directory = await reader.HydrateAsync(new FirmStaffRecord(request.StaffMemberId), token).ConfigureAwait(false);
                if (request.DirectoryResourceOffset >= directory.CommittedStreamPosition)
                    return Result<FirstResponseDescriptor>.Failure(RefuseSource().Error!);
                var directoryRecord = await ReadRecordAsync(directory.Stream, request.DirectoryResourceOffset, token)
                    .ConfigureAwait(false);
                var acceptedRecord = await ReadRecordAsync(engagement.Stream, request.AcceptanceResourceOffset, token)
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
                    return Result<FirstResponseDescriptor>.Failure(RefuseSource().Error!);
                var directoryHistory = await FirmStaffHistory.ReadAsync(events, directory, token).ConfigureAwait(false);
                if (!directoryHistory.Matches(change) || engagement.OperationOf(request.AcceptanceRequestId) is not
                    { Completed: { } completed } || completed.Metadata.EventId != request.AcceptanceEventId ||
                    !completed.Equals(acceptedRecord.Event) || completed.Metadata != acceptedRecord.Event.Metadata)
                    return Result<FirstResponseDescriptor>.Failure(RefuseSource().Error!);
                FirmStaffMemberView staff;
                ServiceEngagementAcceptanceView accepted;
                ServiceEngagementAcceptanceView observed;
                var realm = IndependenceContent.Realm(request.TenantId);
                try
                {
                    staff = await staffViews.ReadAsync(change, token).ConfigureAwait(false);
                    accepted = await IndependenceContent.ReadResultAsync<ServiceEngagementAcceptanceView>(content, realm,
                        originalResult, token).ConfigureAwait(false);
                    observed = engagement.AcceptanceResult is { } current
                        ? await IndependenceContent.ReadResultAsync<ServiceEngagementAcceptanceView>(content, realm, current, token)
                            .ConfigureAwait(false)
                        : accepted;
                }
                catch (ImmutableContentIntegrityException error)
                {
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        $"The directory receipt source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
                }

                if (staff.StaffMemberId != request.StaffMemberId || staff.UserId != request.UserId ||
                    accepted.CompleteServiceHistory.Count != request.AcceptedServiceEpoch ||
                    accepted.EngagementId != request.EngagementId || accepted.TenantId != request.TenantId ||
                    !accepted.Assignments.Any(assignment => assignment.StaffMemberId == staff.StaffMemberId &&
                        assignment.UserId == staff.UserId && assignment.Practice == staff.Practice &&
                        assignment.DirectoryStaffRevision < staff.Revision && assignment.DirectoryStaffRecordedAt <= staff.RecordedAt))
                    return Result<FirstResponseDescriptor>.Failure(RefuseSource().Error!);
                if (!await tenants.IsActiveAsync(request.TenantId, token).ConfigureAwait(false))
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The owning client is inactive; reconciliation remains pending.", isTransient: true));

                var source = new DirectoryStatusSourceView(request.DirectoryEventId, change.OperationId,
                    (long)change.Revision, directoryRecord.ResourceOffset, request.DirectoryPayloadSha256,
                    staff.StaffMemberId, staff.UserId, staff.Practice, staff.Revision, staff.IsActive, staff.RecordedAt);
                var unit = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(request.CommandId, UnitResult.Schema),
                    UnitResult.Schema, new UnitResult(), token).ConfigureAwait(false);
                var response = new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType,
                    UnitResult.Schema, new StandardResponseBody(unit));
                var receipt = new DirectoryIndependenceReevaluationView(
                    Uuid.CreateVersion5(source.RequestId,
                        $"directory_reevaluation_v1:{request.TenantId}:{request.AcceptanceRequestId}:{request.EngagementId}:{staff.StaffMemberId}:{staff.UserId}"),
                    request.TenantId, source, IndependenceSourceDigest.DirectorySource(source), request.AcceptanceRequestId,
                    request.AcceptancePayloadSha256, accepted, IndependenceSourceDigest.Acceptance(accepted), observed,
                    IndependenceSourceDigest.Acceptance(observed), "review_required", true, actor, now);
                receiptContent = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(),
                    "DirectoryIndependenceReevaluated.ReevaluationContent.v1", receipt, token).ConfigureAwait(false);
                result = await AdministratorTransitionEffects.UnitResultAsync(content, realm, request.CommandId, token)
                    .ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(response);
            }, (ServiceEngagementIndependence owner, ImmutableContentRef first) =>
                owner.RecordDirectoryReevaluation(provenance, receiptContent!, first, result!)), context, ct)
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

    static Result RefuseSource() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The directory receipt requires its exact authoritative cause and original client acceptance."));
}
