using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records one authorized accountability operation and preserves its exact original native result.</summary>
public sealed class EscalateWorkItemHandler(WorkAssignmentOperations operations) : IRequestHandler<EscalateWorkItem, WorkItemDetailView>
{
    public ValueTask<Result<WorkItemDetailView>> HandleAsync(IRequestContext<EscalateWorkItem> context, CancellationToken ct) =>
        operations.EscalateAsync(context, ct);
}
