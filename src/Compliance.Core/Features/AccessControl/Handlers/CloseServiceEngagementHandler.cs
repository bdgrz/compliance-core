using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Closes one open service engagement at its current revision.</summary>
sealed class CloseServiceEngagementHandler(ServiceEngagementCommands commands)
    : IRequestHandler<CloseServiceEngagement, ServiceEngagementView>
{
    public ValueTask<Result<ServiceEngagementView>> HandleAsync(IRequestContext<CloseServiceEngagement> context,
        CancellationToken ct) => commands.CloseAsync(context, ct);
}
