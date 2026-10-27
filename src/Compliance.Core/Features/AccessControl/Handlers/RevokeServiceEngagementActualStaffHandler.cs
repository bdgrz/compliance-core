using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Revokes one actual assignment of an accepted service engagement at its current revision.</summary>
sealed class RevokeServiceEngagementActualStaffHandler(ServiceEngagementCommands commands)
    : IRequestHandler<RevokeServiceEngagementActualStaff, ServiceEngagementAcceptanceView>
{
    public ValueTask<Result<ServiceEngagementAcceptanceView>> HandleAsync(
        IRequestContext<RevokeServiceEngagementActualStaff> context, CancellationToken ct) =>
        commands.RevokeAsync(context, ct);
}
