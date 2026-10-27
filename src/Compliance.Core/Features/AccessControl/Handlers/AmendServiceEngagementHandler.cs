using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Amends the open draft of one service engagement at its current revision.</summary>
sealed class AmendServiceEngagementHandler(ServiceEngagementCommands commands)
    : IRequestHandler<AmendServiceEngagement, ServiceEngagementView>
{
    public ValueTask<Result<ServiceEngagementView>> HandleAsync(IRequestContext<AmendServiceEngagement> context,
        CancellationToken ct) => commands.AmendAsync(context, ct);
}
