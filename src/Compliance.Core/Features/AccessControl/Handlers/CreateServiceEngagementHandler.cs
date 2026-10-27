using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Creates the draft of one service engagement on its own owner at revision zero.</summary>
sealed class CreateServiceEngagementHandler(ServiceEngagementCommands commands)
    : IRequestHandler<CreateServiceEngagement, ServiceEngagementView>
{
    public ValueTask<Result<ServiceEngagementView>> HandleAsync(IRequestContext<CreateServiceEngagement> context,
        CancellationToken ct) => commands.CreateAsync(context, ct);
}
