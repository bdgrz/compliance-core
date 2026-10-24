using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

public sealed partial class LinkImpactReviewWorkReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus, IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(SignificantChange.Area), WorkloadName), IReactorHandler<SignificantChangeImpactAssessed>
{
    public const string WorkloadName = "SignificantChangeImpactWorkLinksV2";
    public async ValueTask HandleAsync(IReactorContext<SignificantChangeImpactAssessed> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        if (Pattern.IsTenantTemplate || Pattern.Realm != ev.TenantId.ToString() ||
            context.Source.Stream != new SignificantChange(ev.TenantId, ev.Header.ProgramId, ev.SubjectId).Stream ||
            ev.Metadata.EventId == Uuid.Empty || ev.ChangeCount != 1)
            throw new InvalidOperationException("The work-link reaction requires its exact native assessment source.");
        var mutation = await content.ReadJsonAsync<SignificantChangeMutation>(ev.TenantId.ToString(), ev.Change, ct).ConfigureAwait(false);
        var view = await content.ReadJsonAsync<SignificantChangeView>(ev.TenantId.ToString(), ev.CurrentContent, ct).ConfigureAwait(false);
        if (ev.Change.Schema != SignificantChangeContentStore.ChangeSchema || ev.CurrentContent.Schema != SignificantChangeContentStore.BodySchema ||
            mutation.Kind != nameof(AssessSignificantChange) || !mutation.Changed ||
            mutation.AfterSha256 != AuthoredIntentDigest.Compute(view) || SignificantChangeContentStore.Header(view) != ev.Header ||
            view.CurrentAssessment is not { } assessment || assessment.ContentVersion != ev.Header.CurrentVersionNumber)
            throw new ImmutableContentIntegrityException("The work-link source content differs from its native exact assessment.");
        if (assessment.RelatedReviewWorkIds.Count == 0 && assessment.RelatedCorrectiveActionIds.Count == 0)
            return;
        await ImpactReactionOutcomes.SendAsync(bus, new LinkImpactReviewWork(
            EffectIdentity.CommandId(ev.Metadata.EventId, "change:impact-work-links", ev.SubjectId.ToString(), null),
            ev.TenantId, ev.Header.ProgramId, ev.SubjectId, assessment.ContentVersion), context, ct).ConfigureAwait(false);
    }
}
