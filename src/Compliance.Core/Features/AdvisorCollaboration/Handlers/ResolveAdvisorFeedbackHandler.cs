using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
///     Records the responsible client's closure of one advisor feedback thread at the expected thread revision. A
///     thread is resolved once; a second resolution under another command conflicts.
/// </summary>
public sealed class ResolveAdvisorFeedbackHandler(AdvisorUnitOperation operation, AdvisorFeedbackReader threads,
    TimeProvider clock) : IRequestHandler<ResolveAdvisorFeedback>
{
    const string CommandKind = "ResolveAdvisorFeedback";

    public ValueTask<Result> HandleAsync(IRequestContext<ResolveAdvisorFeedback> context, CancellationToken ct) =>
        AdvisorFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    ValueTask<Result> HandleCoreAsync(IRequestContext<ResolveAdvisorFeedback> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return ValueTask.FromResult(
                AdvisorHandoffRules.Forbidden("Resolving advisor feedback requires a personal HTTP actor."));
        if (request.CommandId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.HandoffId == Uuid.Empty ||
            request.FeedbackId == Uuid.Empty || request.ExpectedRevision < 0)
            return ValueTask.FromResult(AdvisorHandoffRules.Invalid(
                "Advisor feedback resolution requires command, program, handoff, and feedback identities and a non-negative expected revision."));

        var now = clock.GetUtcNow();
        var owner = new AdvisorFeedbackThread(request.TenantId, request.ProgramId, request.HandoffId,
            request.FeedbackId).Stream;
        var provenance = AdvisorContent.Provenance(request.CommandId, CommandKind, owner, actor,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        return operation.ExecuteAsync<AdvisorFeedbackThread>(context, request.TenantId, provenance,
            () => new AdvisorFeedbackThread(request.TenantId, request.ProgramId, request.HandoffId,
                request.FeedbackId, request.CommandId),
            thread => thread.Acceptance,
            (thread, token) => AdvisorFeedbackLookup.RequireMaterializedAsync(threads, thread, request.TenantId,
                request.ProgramId, request.HandoffId, request.FeedbackId, token),
            thread => thread.PrepareResolve(request.ExpectedRevision),
            (thread, frozen, result) => thread.Resolve(provenance, actor, now, frozen, result), ct);
    }
}
