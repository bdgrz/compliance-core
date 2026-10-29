using Microsoft.Extensions.Configuration;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.Controls.Models;

/// <summary>
///     Prevents a new discard event from being emitted until every active reader
///     understands its discriminator.
/// </summary>
public sealed record ControlDraftDiscardReleaseGate(bool IsEnabled)
{
    public static ControlDraftDiscardReleaseGate FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new ControlDraftDiscardReleaseGate(bool.TryParse(
            configuration["Compliance:Controls:DiscardEnabled"], out var enabled) && enabled);
    }
}
