using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Handlers;

/// <summary>Records one authorized accountability operation and preserves its exact original native result.</summary>
public sealed class AssignWorkItemHandler(WorkAssignmentOperations operations) : IRequestHandler<AssignWorkItem, WorkItemDetailView>
{
    public ValueTask<Result<WorkItemDetailView>> HandleAsync(IRequestContext<AssignWorkItem> context, CancellationToken ct) =>
        operations.AssignAsync(context, ct);
}
