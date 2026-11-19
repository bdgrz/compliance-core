using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Aggregates;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Services;

/// <summary>Computes impairment from corrected truth; it never refuses recording that truth.</summary>
public static class IndependenceAssessment
{
    public static Result<IndependenceAssessmentView> Evaluate(Uuid tenantId, IndependenceRuleSetView rules,
        IReadOnlyList<NonattestServiceRecordView> services, DateOnly examinationPeriodStart,
        DateOnly examinationPeriodEnd)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(services);
        if (tenantId == Uuid.Empty || !rules.IsRatified || rules.Version <= 0 ||
            !IndependenceRuleSet.ValidTerms(rules.Terms) || examinationPeriodStart == default ||
            examinationPeriodEnd < examinationPeriodStart || !ValidSources(tenantId, services))
            return Invalid();
        DateOnly windowStart;
        try
        {
            windowStart = examinationPeriodStart.AddMonths(-rules.Terms.LookBackMonths);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Invalid();
        }

        var classifications = rules.Terms.Classifications.ToDictionary(static value => value.ServiceKind,
            static value => value.Classification, StringComparer.Ordinal);
        var impairing = false;
        var conditional = false;
        foreach (var service in services.Where(static value => !value.IsVoided))
        {
            var classification = classifications[service.Terms.ServiceKind];
            conditional |= classification == "conditionally_compatible";
            impairing |= classification == "impairing" && service.Terms.StartedOn <= examinationPeriodEnd &&
                (service.Terms.EndedOn is null || service.Terms.EndedOn >= windowStart);
        }
        var result = impairing ? "impaired" : conditional ? "conditionally_compatible" : "compatible";
        return Result<IndependenceAssessmentView>.Success(new(tenantId, rules.Version,
            examinationPeriodStart, examinationPeriodEnd, result, Versions(services)));
    }

    public static bool MatchesCurrentSources(IndependenceAssessmentView assessment,
        IndependenceRuleSetView currentRules, IReadOnlyList<NonattestServiceRecordView> currentServices)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        ArgumentNullException.ThrowIfNull(currentRules);
        ArgumentNullException.ThrowIfNull(currentServices);
        return currentRules.IsRatified && assessment.RuleSetVersion == currentRules.Version &&
            ValidSources(assessment.TenantId, currentServices) && assessment.ServicesConsidered is not null &&
            assessment.ServicesConsidered.SequenceEqual(Versions(currentServices));
    }

    static bool ValidSources(Uuid tenantId, IReadOnlyList<NonattestServiceRecordView> services) =>
        services.All(value => value is not null && value.TenantId == tenantId && value.RecordId != Uuid.Empty &&
            value.Revision > 0 && NonattestServiceRecord.ValidTerms(value.Terms)) &&
        services.Select(static value => value.RecordId).Distinct().Count() == services.Count;

    static IReadOnlyList<NonattestServiceVersion> Versions(IReadOnlyList<NonattestServiceRecordView> services) =>
        Array.AsReadOnly(services.OrderBy(static value => value.RecordId.ToString(), StringComparer.Ordinal)
            .Select(static value => new NonattestServiceVersion(value.RecordId, value.Revision)).ToArray());

    static Result<IndependenceAssessmentView> Invalid() => Result<IndependenceAssessmentView>.Failure(
        new(RequestErrorKind.Validation, "Independence needs ratified rules, an ordered examination period and an exact client service history."));
}
