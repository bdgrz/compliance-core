using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Models;

/// <summary>A workforce person whose frozen roster facts match a campaign audience rule.</summary>
public sealed record RosterMatch(Uuid PersonId, string DisplayName, string WorkerType,
    string? Department, DateOnly StartDate);
