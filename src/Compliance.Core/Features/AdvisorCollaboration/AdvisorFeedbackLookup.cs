using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
///     Tells a feedback thread that does not exist from one that the handoff admitted and that is not materialized
///     yet. The admission is the authority: an admitted thread is only waiting for its independent materialization.
/// </summary>
static class AdvisorFeedbackLookup
{
    /// <summary>Success when the thread exists; a retryable conflict when it is admitted; otherwise not found.</summary>
    public static async ValueTask<Result> RequireMaterializedAsync(IAggregateReader reader, AdvisorFeedbackThread thread,
        Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId, CancellationToken ct)
    {
        if (thread.IsMaterialized)
            return Result.Success;
        var handoff = await reader.HydrateAsync(new AdvisorHandoff(tenantId, programId, handoffId), ct)
            .ConfigureAwait(false);
        return AdvisorHandoffRules.InProgram(handoff, programId) && handoff.HasAdmitted(feedbackId)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The advisor feedback is not materialized yet; retry shortly.", true))
            : Result.Failure(new RequestError(RequestErrorKind.NotFound, "The advisor feedback was not found."));
    }
}
