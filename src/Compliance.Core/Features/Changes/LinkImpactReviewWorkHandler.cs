using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

public sealed class LinkImpactReviewWorkHandler(IAggregateExecutor executor,
    IAggregateReader reader, IChangeImpactWorkOwnershipReader ownership, TimeProvider clock)
    : IRequestHandler<LinkImpactReviewWork>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<LinkImpactReviewWork> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Impact-review work links may only be written by a trusted reactor."));
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.ChangeId == Uuid.Empty || request.ContentVersion <= 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Tenant, program, change, and assessment-version identities are required."));

        var observed = await reader.HydrateAsync(new SignificantChange(request.TenantId,
            request.ProgramId, request.ChangeId), ct).ConfigureAwait(false);
        if (observed.Current is not { CurrentAssessment: { } assessment } state ||
            assessment.ContentVersion != request.ContentVersion)
            return Result.Failure(NotCurrent(observed, request.ContentVersion));
        var workItemIds = assessment.RelatedReviewWorkIds.Concat(
                assessment.RelatedCorrectiveActionIds)
            .OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray();
        if (workItemIds.Length == 0)
            return Result.Success;

        var ownershipResult = await ownership.ConfirmOwnedByProgramAsync(request.TenantId,
            request.ProgramId, workItemIds, ct).ConfigureAwait(false);
        if (!ownershipResult.IsSuccess)
            return ownershipResult;

        return await executor.ExecuteAsync(new SignificantChange(request.TenantId,
                request.ProgramId, request.ChangeId), change =>
            {
                if (change.Current is not { CurrentAssessment: { } currentAssessment } current ||
                    currentAssessment.ContentVersion != request.ContentVersion ||
                    !currentAssessment.RelatedReviewWorkIds.Concat(
                            currentAssessment.RelatedCorrectiveActionIds)
                        .OrderBy(static id => id.ToString(), StringComparer.Ordinal)
                        .SequenceEqual(workItemIds))
                    return AggregateOutcome.Discard(Result.Failure(NotCurrent(change, request.ContentVersion)));
                return CommandFailureRequestAdapter.ToOutcome(change.LinkReviewWork(change.Revision,
                    workItemIds, assessment.AssessedByMemberId, assessment.AssessedByDisplay,
                    clock.GetUtcNow()));
            }, context, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Separates an assessment that a newer content version has replaced (terminal) or that the owner does not show
    ///     yet (transient) from a request that never matched this change.
    /// </summary>
    static RequestError NotCurrent(SignificantChange change, int contentVersion)
    {
        if (change.Current is not { } current)
            return new RequestError(RequestErrorKind.Conflict,
                "The requested significant-change assessment is no longer current.");
        if (contentVersion > current.CurrentVersionNumber)
            return ImpactReactionOutcomes.NotReady("The requested significant-change assessment is not visible yet.");
        return current.Assessments.Any(item => item.ContentVersion == contentVersion) &&
               contentVersion < current.CurrentVersionNumber
            ? ImpactReactionOutcomes.Superseded
            : new RequestError(RequestErrorKind.Conflict,
                "The requested significant-change assessment is no longer current.");
    }
}
