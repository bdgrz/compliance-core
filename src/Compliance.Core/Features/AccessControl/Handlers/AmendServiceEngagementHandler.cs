using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Amends the open draft of one service engagement at its current revision.</summary>
sealed class AmendServiceEngagementHandler(ServiceEngagementCommands commands)
    : IRequestHandler<AmendServiceEngagement, ServiceEngagementView>
{
    public ValueTask<Result<ServiceEngagementView>> HandleAsync(IRequestContext<AmendServiceEngagement> context,
        CancellationToken ct) => commands.AmendAsync(context, ct);
}
