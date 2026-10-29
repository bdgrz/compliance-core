using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Withdraws the current tentative proposal of one staff member from an open draft engagement.</summary>
sealed class WithdrawServiceEngagementStaffProposalHandler(ServiceEngagementCommands commands)
    : IRequestHandler<WithdrawServiceEngagementStaffProposal, ServiceEngagementView>
{
    public ValueTask<Result<ServiceEngagementView>> HandleAsync(
        IRequestContext<WithdrawServiceEngagementStaffProposal> context, CancellationToken ct) =>
        commands.WithdrawAsync(context, ct);
}
