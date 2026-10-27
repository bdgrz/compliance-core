using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Decodes one exact immutable business snapshot and binds it to its attributed native envelope.</summary>
public static class ExaminationEngagementContentReader
{
    public static async Task<ExaminationEngagementState> ReadAsync(IImmutableContentStore content,
        DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is not IExaminationEngagementFact fact || fact.Change.Schema != ExaminationEngagementState.Schema)
            throw Invalid("The engagement business content has another native source kind/schema.");
        var state = await content.ReadJsonAsync<ExaminationEngagementState>(fact.TenantId.ToString(), fact.Change, ct)
            .ConfigureAwait(false);
        var view = state.Engagement;
        var frozen = domainEvent is TypeIBaselineFrozen or TypeIBaselineAmended;
        if (view.TenantId != fact.TenantId || view.ProgramId != fact.ProgramId ||
            view.ExaminationEngagementId != fact.SubjectId || view.Revision != fact.Revision ||
            view.EngagementType != fact.EngagementType || view.BoundaryId != fact.BoundaryId ||
            view.Status != (frozen ? "baseline_frozen" : "draft") || view.UpdatedAt != fact.OccurredAt ||
            view.ActorMemberId != fact.ActorMemberId || view.ActorDisplay != fact.Operation.Actor.Display ||
            view.ActorMemberId != ExaminationEngagement.Member(fact.Operation) ||
            frozen != (state.Baseline is not null))
            throw Invalid("The immutable engagement snapshot differs from its exact native scope, actor or revision.");
        if (state.Baseline is { } baseline && (baseline.TenantId != fact.TenantId || baseline.ProgramId != fact.ProgramId ||
            baseline.ExaminationEngagementId != fact.SubjectId || baseline.TypeIBaselineId != fact.BaselineId ||
            baseline.Revision != fact.BaselineRevision || baseline.ActorMemberId != fact.ActorMemberId ||
            baseline.ActorDisplay != fact.Operation.Actor.Display || baseline.UpdatedAt != fact.OccurredAt ||
            baseline.Status != (domainEvent is TypeIBaselineFrozen ? "frozen" : "amended")))
            throw Invalid("The immutable baseline differs from its exact native scope, identity, actor or revision.");
        return state;
    }

    internal static ExaminationEngagementRuleChange RuleChange(DomainEvent fact, ExaminationEngagementState state) => fact switch
    {
        ExaminationEngagementOpened source => new ExaminationEngagementRuleOpened(source.TenantId, state.Engagement, source.OccurredAt),
        ExaminationEngagementRevised source => new ExaminationEngagementRuleRevised(source.TenantId, state.Engagement, source.OccurredAt),
        TypeIBaselineFrozen source => new ExaminationEngagementRuleFrozen(source.TenantId, state.Engagement, state.Baseline!, source.OccurredAt),
        TypeIBaselineAmended source => new ExaminationEngagementRuleAmended(source.TenantId, state.Engagement, state.Baseline!, source.OccurredAt),
        _ => throw Invalid("The engagement source kind is unsupported.")
    };
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
