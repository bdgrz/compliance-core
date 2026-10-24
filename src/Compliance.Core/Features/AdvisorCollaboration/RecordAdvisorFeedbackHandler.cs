using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
///     Admits attributed consultant feedback on an exact selected version while the handoff is ready for review. The
///     complete contribution and the selection generation are frozen on the handoff before the 202 acknowledgment is
///     frozen on the admission, so an exact retry replays the original acknowledgment even after the handoff was
///     revised, revoked or closed; <c>IndependentChildMaterialization</c> then materializes the thread. Admission
///     shares the handoff stream with revocation and closure, so one of two racing commands is refused by the
///     stream's revision, and feedback admitted first is never discarded by a later revocation.
/// </summary>
public sealed class RecordAdvisorFeedbackHandler(NativeOperationExecutor operations, IAggregateReader reader,
    AdvisorHandoffReader handoffs, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RecordAdvisorFeedback, OperationAcceptedResponse>
{
    const string CommandKind = "RecordAdvisorFeedback";

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<RecordAdvisorFeedback> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return Failure(RequestErrorKind.Forbidden, "Recording advisor feedback requires a personal HTTP actor.");
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.HandoffId == Uuid.Empty || request.FeedbackId == Uuid.Empty ||
            request.RecordId == Uuid.Empty || request.ExpectedHandoffRevision < 0 ||
            request.SelectedSource is null && request.RecordVersionId == Uuid.Empty ||
            request.SelectedSource is { } selected && (!selected.IsSupported ||
                selected.RecordId != request.RecordId || selected.VersionId != request.RecordVersionId) ||
            string.IsNullOrWhiteSpace(request.Kind) || string.IsNullOrWhiteSpace(request.Body))
            return Failure(RequestErrorKind.Validation,
                "Advisor feedback requires tenant, program, handoff, feedback, record, version, kind, body and a non-negative expected handoff revision.");

        var sourceVersion = request.SelectedSource ?? new AdvisorRecordVersion(request.RecordId, request.RecordVersionId);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var canonical = request with { Body = request.Body.Trim() };
        var snapshot = await reader.HydrateAsync(new AdvisorHandoff(request.TenantId, request.ProgramId,
            request.HandoffId, request.FeedbackId), ct).ConfigureAwait(false);
        var provenance = AdvisorContent.Provenance(request.FeedbackId, CommandKind, snapshot.Stream, actor,
            (ulong)request.ExpectedHandoffRevision, AuthoredIntentDigest.Compute(canonical), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (!AdvisorHandoffRules.InProgram(snapshot, request.ProgramId))
                return Failure(RequestErrorKind.NotFound, "The advisor handoff was not found in the requested program.");
            var prepared = snapshot.PrepareAdmit(request.ExpectedHandoffRevision, today);
            if (!prepared.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(prepared.Error!);
            if (snapshot.HasAdmitted(request.FeedbackId))
                return Failure(RequestErrorKind.Conflict, "The feedback identifier already has different content.");
            if (!AdvisorHandoffRules.FeedbackKinds.Contains(request.Kind) || !sourceVersion.HasValidIdentity)
                return Failure(RequestErrorKind.Validation,
                    "Feedback must target a selected exact version and include a supported kind, body, and author.");
            // The revision equals the expected one, so this is the selection the admission is accepted against.
            var current = await handoffs.ReadAsync(snapshot, request.TenantId, ct).ConfigureAwait(false);
            if (!current.Selection.Contains(sourceVersion))
                return Failure(RequestErrorKind.Validation,
                    "Feedback must target a selected exact version and include a supported kind, body, and author.");

            var authored = await AdvisorContent.WriteAsync(content, request.TenantId,
                AdvisorContent.FeedbackAuthoredSchema, canonical, ct).ConfigureAwait(false);
            intent = await AdvisorContent.WriteAsync(content, request.TenantId,
                ChildAdmissionIntent.Schema, new ChildAdmissionIntent(AdvisorContent.Header(provenance, now),
                    request.HandoffId, (ulong)snapshot.Generation, AdmittedChildKind.AdvisorFeedback,
                    request.FeedbackId, authored,
                    await AdvisorContent.EmptyProofsAsync(content, request.TenantId, request.FeedbackId, ct)
                        .ConfigureAwait(false), (ulong)request.ExpectedHandoffRevision + 1), ct).ConfigureAwait(false);
            response = await AdvisorContent.AcceptedResponseAsync(content, request.TenantId, request.FeedbackId,
                RegisterFirmStaffHandler.StatusUrlTemplate, now, ct)
                .ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AdvisorHandoff>(binding,
            () => new AdvisorHandoff(request.TenantId, request.ProgramId, request.HandoffId, request.FeedbackId),
            handoff => handoff.Acceptance,
            handoff => AdvisorContent.Prepared(handoff.PrepareAdmit(request.ExpectedHandoffRevision, today), response),
            (AdvisorHandoff handoff, ImmutableContentRef acknowledgment) =>
                handoff.AdmitFeedback(provenance, intent!, today, acknowledgment)), context, ct)
            .ConfigureAwait(false);
        return await AdvisorContent.AcknowledgmentAsync(content, request.TenantId, outcome, ct).ConfigureAwait(false);
    }

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
