using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

public sealed partial class LinkImpactReviewWorkReactor(IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("significant-changes"), WorkloadName),
      IReactorHandler<SignificantChangeImpactAssessed>
{
    public const string WorkloadName = "SignificantChangeImpactWorkLinksV1";

    public ValueTask HandleAsync(IReactorContext<SignificantChangeImpactAssessed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        if (Pattern.IsTenantTemplate || Pattern.Realm != ev.TenantId.ToString() ||
            context.Source.Stream != new SignificantChange(ev.TenantId,
                ev.State.ProgramId, ev.SubjectId).Stream ||
            ev.State.TenantId != ev.TenantId || ev.State.SignificantChangeId != ev.SubjectId ||
            ev.State.Revision != ev.Revision ||
            ev.State.CurrentAssessment is not { } assessment ||
            assessment.ContentVersion != ev.State.CurrentVersionNumber)
            throw new InvalidOperationException(
                "A work-link reaction must originate from a current significant-change assessment.");

        if (assessment.RelatedReviewWorkIds.Count == 0 &&
            assessment.RelatedCorrectiveActionIds.Count == 0)
            return ValueTask.CompletedTask;

        return bus.SendReactionAsync(new LinkImpactReviewWork(ev.TenantId,
            ev.State.ProgramId, ev.SubjectId, assessment.ContentVersion), context, ct);
    }
}
