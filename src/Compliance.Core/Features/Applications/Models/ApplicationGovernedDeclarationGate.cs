using Microsoft.Extensions.Configuration;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>Requires complete fresh declarations after compatible readers and writers are deployed.</summary>
public sealed record ApplicationGovernedDeclarationGate(bool IsEnabled)
{
    public static ApplicationGovernedDeclarationGate FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new ApplicationGovernedDeclarationGate(bool.TryParse(
            configuration["Compliance:Applications:GovernedDeclarationsEnabled"], out var enabled) && enabled);
    }
}
