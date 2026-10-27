using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records one authorized accountability operation and preserves its exact original native result.</summary>
public sealed class AssignWorkItemHandler(WorkAssignmentOperations operations) : IRequestHandler<AssignWorkItem, WorkItemDetailView>
{
    public ValueTask<Result<WorkItemDetailView>> HandleAsync(IRequestContext<AssignWorkItem> context, CancellationToken ct) =>
        operations.AssignAsync(context, ct);
}
