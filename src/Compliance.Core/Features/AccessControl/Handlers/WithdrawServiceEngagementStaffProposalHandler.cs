using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Withdraws the current tentative proposal of one staff member from an open draft engagement.</summary>
sealed class WithdrawServiceEngagementStaffProposalHandler(ServiceEngagementCommands commands)
    : IRequestHandler<WithdrawServiceEngagementStaffProposal, ServiceEngagementView>
{
    public ValueTask<Result<ServiceEngagementView>> HandleAsync(
        IRequestContext<WithdrawServiceEngagementStaffProposal> context, CancellationToken ct) =>
        commands.WithdrawAsync(context, ct);
}
