using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>
///     The pure historical evaluation of one client: the complete service history, one draft rule version and an
///     examination date. It reads nothing else, so the same frozen snapshot always yields the same preview, and the
///     preview never grants production acceptance.
/// </summary>
static class ClientIndependencePreviewEvaluator
{
    public static Result<IndependenceEvaluationView> Evaluate(Uuid tenantId, Uuid evaluationId,
        IndependenceRuleVersionView version, DateOnly periodStart, IReadOnlyList<NonattestServiceView> services,
        ActorReference actor, DateTimeOffset recordedAt)
    {
        if (tenantId == Uuid.Empty || evaluationId == Uuid.Empty || periodStart == DateOnly.MinValue ||
            version is null || version.IsRatified || version.Version <= 0 ||
            !IndependenceRecordValidation.ValidRules(version.Content) ||
            !IndependenceRecordValidation.ValidAttribution(evaluationId, actor, recordedAt, "member"))
            return Failure(RequestErrorKind.Validation,
                "An evaluation requires an owning client, immutable identity, examination period and member attribution.");
        version = IndependenceRecordValidation.Freeze(version);
        var history = services.Select(IndependenceRecordValidation.Freeze).ToArray();
        var source = history.Select(service => new NonattestServiceRecord(tenantId,
            service.Content.ServiceEngagementId, service.Content.ServiceType, service.Content.StartedOn,
            service.Content.EndedOn, service.Content.FirmStaffMemberIds, service.Content.InvolvedManagementFunctions))
            .ToArray();
        var policy = new IndependenceRuleSet(version.Version, version.Content.LookBackMonths,
            version.Content.ServiceRules.Select(rule => new IndependenceServiceRule(rule.ServiceType,
                IndependenceWire.Classification(rule.Classification),
                IndependenceWire.Classification(rule.ManagementFunctionsClassification))).ToArray());
        var decision = IndependenceCompartments.CanAcceptAttestEngagement(tenantId, policy, source, periodStart,
            partnerEvaluationRecorded: false);
        if (decision.Code is IndependenceDecisionCode.EvaluationInputInvalid or IndependenceDecisionCode.RuleSetInvalid or
            IndependenceDecisionCode.ServiceHistoryInvalid)
            return Failure(RequestErrorKind.Validation,
                "The examination date cannot be evaluated against the retained rule and service history.");
        var considered = decision.ConsideredServices.Select(assessment => assessment.Service).ToHashSet();
        return Result<IndependenceEvaluationView>.Success(new IndependenceEvaluationView(evaluationId, tenantId,
            version.Version, version, periodStart, IndependenceWire.Code(decision.Code),
            decision.EvaluationOutcome switch
            {
                IndependenceEvaluationOutcome.Compatible => "compatible",
                IndependenceEvaluationOutcome.ConditionallyCompatible => "conditionally_compatible",
                IndependenceEvaluationOutcome.Impaired => "impaired",
                _ => null,
            }, Array.AsReadOnly(history), Array.AsReadOnly(history
                .Where((_, index) => considered.Contains(source[index]))
                .Select(service => service.ServiceRecordId).ToArray()), actor, recordedAt, true));
    }

    static Result<IndependenceEvaluationView> Failure(RequestErrorKind kind, string message) =>
        Result<IndependenceEvaluationView>.Failure(new RequestError(kind, message));
}
