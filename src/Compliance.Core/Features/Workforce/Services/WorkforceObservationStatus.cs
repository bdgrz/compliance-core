using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Services;

/// <summary>Derives an observation's status from its closure: open until resolved or dismissed.</summary>
public static class WorkforceObservationStatus
{
    public static string Of(WorkforceObservationResolutionView? resolution) =>
        resolution?.Resolution ?? "open";
}
