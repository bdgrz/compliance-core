using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Changes.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.Changes.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Changes.Handlers;

public sealed class LinkImpactReviewWorkHandler(SignificantChangeOperations operations,
    IChangeImpactWorkOwnershipReader ownership) : IRequestHandler<LinkImpactReviewWork, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<LinkImpactReviewWork> context, CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return ValueTask.FromResult(Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Impact-review work links may only be written by a trusted reactor.")));
        var request = context.Request;
        if (request.ContentVersion <= 0)
            return ValueTask.FromResult(Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Validation,
                "The exact assessment content version is required.")));
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.ChangeId, request.CommandId,
            nameof(LinkImpactReviewWork), null, ActorReference.ForSystemProcess("reactor:LinkImpactReviewWork", "LinkImpactReviewWork"),
            async (snapshot, operation, token) =>
            {
                if (snapshot.View is not { CurrentAssessment: { } assessment } state ||
                    assessment.ContentVersion != request.ContentVersion)
                    return Result.Failure(NotCurrent(snapshot.View, request.ContentVersion));
                var ids = assessment.RelatedReviewWorkIds.Concat(assessment.RelatedCorrectiveActionIds)
                    .OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray();
                var ready = await ownership.ConfirmOwnedByProgramAsync(request.TenantId, request.ProgramId, ids, token).ConfigureAwait(false);
                return !ready.IsSuccess ? ready : SignificantChangeOperations.Outcome(snapshot.Rules.LinkReviewWork(
                    state.Revision, ids, assessment.AssessedByMemberId, assessment.AssessedByDisplay, operation.AcceptedAt));
            }, ct);
    }

    /// <summary>
    ///     Separates an assessment that a newer content version has replaced (terminal) or that the owner does not show
    ///     yet (transient) from a request that never matched this change.
    /// </summary>
    static RequestError NotCurrent(SignificantChangeView? current, int contentVersion)
    {
        if (current is null)
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
