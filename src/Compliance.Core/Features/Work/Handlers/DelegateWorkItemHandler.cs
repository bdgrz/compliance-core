using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records one authorized accountability operation and preserves its exact original native result.</summary>
public sealed class DelegateWorkItemHandler(WorkAssignmentOperations operations) : IRequestHandler<DelegateWorkItem, WorkItemDetailView>
{
    public ValueTask<Result<WorkItemDetailView>> HandleAsync(IRequestContext<DelegateWorkItem> context, CancellationToken ct) =>
        operations.DelegateAsync(context, ct);
}
