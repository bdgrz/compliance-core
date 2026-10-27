using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Tentatively proposes one active staff member for the open draft of a service engagement.</summary>
sealed class ProposeServiceEngagementStaffHandler(ServiceEngagementCommands commands)
    : IRequestHandler<ProposeServiceEngagementStaff, ServiceEngagementView>
{
    public ValueTask<Result<ServiceEngagementView>> HandleAsync(
        IRequestContext<ProposeServiceEngagementStaff> context, CancellationToken ct) =>
        commands.ProposeAsync(context, ct);
}
