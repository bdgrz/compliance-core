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
///     Records the client's response on a feedback thread at the expected thread revision. The thread, not the
///     handoff, owns responses, so a response is accepted after the handoff was revoked or closed.
/// </summary>
public sealed class RespondToAdvisorFeedbackHandler(AdvisorUnitOperation operation, AdvisorFeedbackReader threads,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<RespondToAdvisorFeedback>
{
    const string CommandKind = "RespondToAdvisorFeedback";

    public ValueTask<Result> HandleAsync(IRequestContext<RespondToAdvisorFeedback> context, CancellationToken ct) =>
        AdvisorFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    ValueTask<Result> HandleCoreAsync(IRequestContext<RespondToAdvisorFeedback> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return ValueTask.FromResult(
                AdvisorHandoffRules.Forbidden("Responding to advisor feedback requires a personal HTTP actor."));
        if (request.CommandId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.HandoffId == Uuid.Empty ||
            request.FeedbackId == Uuid.Empty || request.ExpectedRevision < 0)
            return ValueTask.FromResult(AdvisorHandoffRules.Invalid(
                "Advisor feedback responses require command, program, handoff and feedback identities and a non-negative expected revision."));
        if (string.IsNullOrWhiteSpace(request.Response))
            return ValueTask.FromResult(AdvisorHandoffRules.Invalid("A feedback response requires content and an actor."));

        var now = clock.GetUtcNow();
        var canonical = request with { Response = request.Response.Trim() };
        var owner = new AdvisorFeedbackThread(request.TenantId, request.ProgramId, request.HandoffId,
            request.FeedbackId).Stream;
        var provenance = AdvisorContent.Provenance(request.CommandId, CommandKind, owner, actor,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(canonical), now);
        ImmutableContentRef? authored = null;
        return operation.ExecuteAsync<AdvisorFeedbackThread>(context, request.TenantId, provenance,
            () => new AdvisorFeedbackThread(request.TenantId, request.ProgramId, request.HandoffId,
                request.FeedbackId, request.CommandId),
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
            (thread, frozen, result) => thread.Respond(provenance, authored!, actor, now, frozen, result), ct);
    }
}
