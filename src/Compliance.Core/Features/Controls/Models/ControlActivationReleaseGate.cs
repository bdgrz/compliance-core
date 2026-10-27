using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.Controls.Models;

/// <summary>
///     Prevents control review and approval events from being emitted until every active reader
///     understands their discriminators.
/// </summary>
public sealed record ControlActivationReleaseGate(bool IsEnabled)
{
    public static ControlActivationReleaseGate FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new ControlActivationReleaseGate(bool.TryParse(
            configuration["Compliance:Controls:ActivationEnabled"], out var enabled) && enabled);
    }

    internal static RequestError Unavailable => new(RequestErrorKind.Conflict,
        "Control review and activation are unavailable until all active event readers are upgraded.");
}
