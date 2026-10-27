using Microsoft.Extensions.Configuration;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>Requires complete resulting content for application revisions when explicitly enabled.</summary>
public sealed record ApplicationGovernedRevisionGate(bool IsEnabled)
{
    public static ApplicationGovernedRevisionGate FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new ApplicationGovernedRevisionGate(bool.TryParse(
            configuration["Compliance:Applications:GovernedRevisionsEnabled"], out var enabled) && enabled);
    }
}
