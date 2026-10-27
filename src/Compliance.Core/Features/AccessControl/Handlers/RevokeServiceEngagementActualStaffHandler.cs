using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Revokes one actual assignment of an accepted service engagement at its current revision.</summary>
sealed class RevokeServiceEngagementActualStaffHandler(ServiceEngagementCommands commands)
    : IRequestHandler<RevokeServiceEngagementActualStaff, ServiceEngagementAcceptanceView>
{
    public ValueTask<Result<ServiceEngagementAcceptanceView>> HandleAsync(
        IRequestContext<RevokeServiceEngagementActualStaff> context, CancellationToken ct) =>
        commands.RevokeAsync(context, ct);
}
