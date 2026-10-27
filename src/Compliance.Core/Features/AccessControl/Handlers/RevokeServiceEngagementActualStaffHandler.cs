using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Revokes one actual assignment of an accepted service engagement at its current revision.</summary>
sealed class RevokeServiceEngagementActualStaffHandler(ServiceEngagementCommands commands)
    : IRequestHandler<RevokeServiceEngagementActualStaff, ServiceEngagementAcceptanceView>
{
    public ValueTask<Result<ServiceEngagementAcceptanceView>> HandleAsync(
        IRequestContext<RevokeServiceEngagementActualStaff> context, CancellationToken ct) =>
        commands.RevokeAsync(context, ct);
}
