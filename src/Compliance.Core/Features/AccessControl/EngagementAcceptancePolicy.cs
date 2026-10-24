using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The independence policy an acceptance is judged by: an advisory engagement is always allowed, an attest
///     engagement is judged against the complete service history within the look-back of the ratified rules, and a
///     conditionally compatible history requires the exact recorded partner evaluation. Impairment and unclassified
///     services can never be overridden.
/// </summary>
static class EngagementAcceptancePolicy
{
    /// <summary>The decision over the complete history, with the identities of the services it considered.</summary>
    public static (IndependenceDecision Decision, IReadOnlyList<Uuid> ConsideredServiceRecordIds) Evaluate(
        Uuid tenantId, string practice, IndependenceRuleVersionView rules, IReadOnlyList<NonattestServiceView> services,
        DateOnly periodStart, bool partnerEvaluationRecorded)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(services);
        if (practice == PracticeIndependenceCommitment.Advisory)
            return (IndependenceDecision.Allow, []);
        var policy = new IndependenceRuleSet(rules.Version, rules.Content.LookBackMonths,
            rules.Content.ServiceRules.Select(rule => new IndependenceServiceRule(rule.ServiceType,
                IndependenceWire.Classification(rule.Classification),
                IndependenceWire.Classification(rule.ManagementFunctionsClassification))).ToArray());
        var sources = services.Select(service => new NonattestServiceRecord(tenantId,
            service.Content.ServiceEngagementId, service.Content.ServiceType, service.Content.StartedOn,
            service.Content.EndedOn, service.Content.FirmStaffMemberIds,
            service.Content.InvolvedManagementFunctions)).ToArray();
        var decision = IndependenceCompartments.CanAcceptAttestEngagement(tenantId, policy, sources, periodStart,
            partnerEvaluationRecorded);
        var considered = decision.ConsideredServices.Select(assessment => assessment.Service).ToHashSet();
        return (decision, Array.AsReadOnly(services.Where((_, index) => considered.Contains(sources[index]))
            .Select(service => service.ServiceRecordId).ToArray()));
    }
}
