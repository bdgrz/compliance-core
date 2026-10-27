using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Handlers;

/// <summary>Records one authorized accountability operation and preserves its exact original native result.</summary>
public sealed class EscalateWorkItemHandler(WorkAssignmentOperations operations) : IRequestHandler<EscalateWorkItem, WorkItemDetailView>
{
    public ValueTask<Result<WorkItemDetailView>> HandleAsync(IRequestContext<EscalateWorkItem> context, CancellationToken ct) =>
        operations.EscalateAsync(context, ct);
}
