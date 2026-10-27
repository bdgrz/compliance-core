using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Rules;

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
