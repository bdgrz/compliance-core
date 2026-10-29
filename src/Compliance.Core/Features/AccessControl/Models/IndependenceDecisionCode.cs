using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>A stable Compliance policy outcome for independence checks.</summary>
public enum IndependenceDecisionCode
{
    Allowed,
    PersonPracticeConflict,
    AssignmentInvalid,
    EvaluationInputInvalid,
    ServiceHistoryInvalid,
    RecentImpairingService,
    PartnerEvaluationRequired,
    RuleSetInvalid,
    ServiceNotClassified,
}
