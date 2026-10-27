using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Handlers;

/// <summary>
///     Appends an attributed correction to feedback on its own thread at the expected thread revision. The correction
///     identity is the logical operation; the original contribution is preserved and the correction names its author
///     and reason. A thread that is admitted but not materialized yet is a retryable conflict.
/// </summary>
public sealed class CorrectAdvisorFeedbackHandler(AdvisorUnitOperation operation, AdvisorFeedbackReader threads,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<CorrectAdvisorFeedback>
{
    const string CommandKind = "CorrectAdvisorFeedback";

    public ValueTask<Result> HandleAsync(IRequestContext<CorrectAdvisorFeedback> context, CancellationToken ct) =>
        AdvisorFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    ValueTask<Result> HandleCoreAsync(IRequestContext<CorrectAdvisorFeedback> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return ValueTask.FromResult(
                AdvisorHandoffRules.Forbidden("Correcting advisor feedback requires a personal HTTP actor."));
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.HandoffId == Uuid.Empty || request.FeedbackId == Uuid.Empty ||
            request.CorrectionId == Uuid.Empty || request.ExpectedRevision < 0 ||
            string.IsNullOrWhiteSpace(request.CorrectedBody) || string.IsNullOrWhiteSpace(request.CorrectionReason))
            return ValueTask.FromResult(AdvisorHandoffRules.Invalid(
                "A feedback correction requires tenant, program, handoff, feedback, correction, and content identities and a non-negative expected revision."));

        var now = clock.GetUtcNow();
        var canonical = request with
        {
            CorrectedBody = request.CorrectedBody.Trim(),
            CorrectionReason = request.CorrectionReason.Trim(),
        };
        var owner = new AdvisorFeedbackThread(request.TenantId, request.ProgramId, request.HandoffId,
            request.FeedbackId).Stream;
        var provenance = AdvisorContent.Provenance(request.CorrectionId, CommandKind, owner, actor,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(canonical), now);
        ImmutableContentRef? authored = null;
        return operation.ExecuteAsync<AdvisorFeedbackThread>(context, request.TenantId, provenance,
            () => new AdvisorFeedbackThread(request.TenantId, request.ProgramId, request.HandoffId,
                request.FeedbackId, request.CorrectionId),
            thread => thread.Acceptance,
            async (thread, token) =>
            {
                var materialized = await AdvisorFeedbackLookup.RequireMaterializedAsync(threads, thread, request.TenantId,
                    request.ProgramId, request.HandoffId, request.FeedbackId, token).ConfigureAwait(false);
                if (!materialized.IsSuccess)
                    return materialized;
                authored = await AdvisorAuthored.WriteAsync(content, request.TenantId, canonical, token).ConfigureAwait(false);
                return Result.Success;
            },
            thread => thread.PrepareWrite(request.ExpectedRevision),
            (thread, frozen, result) => thread.Correct(provenance, request.CorrectionId, authored!, actor, now, frozen, result), ct);
    }
}
