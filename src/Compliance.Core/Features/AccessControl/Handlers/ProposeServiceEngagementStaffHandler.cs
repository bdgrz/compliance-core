using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Tentatively proposes one active staff member for the open draft of a service engagement.</summary>
sealed class ProposeServiceEngagementStaffHandler(ServiceEngagementCommands commands)
    : IRequestHandler<ProposeServiceEngagementStaff, ServiceEngagementView>
{
    public ValueTask<Result<ServiceEngagementView>> HandleAsync(
        IRequestContext<ProposeServiceEngagementStaff> context, CancellationToken ct) =>
        commands.ProposeAsync(context, ct);
}
