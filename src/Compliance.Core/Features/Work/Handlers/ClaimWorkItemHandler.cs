using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records one authorized accountability operation and preserves its exact original native result.</summary>
public sealed class ClaimWorkItemHandler(WorkAssignmentOperations operations) : IRequestHandler<ClaimWorkItem, WorkItemDetailView>
{
    public ValueTask<Result<WorkItemDetailView>> HandleAsync(IRequestContext<ClaimWorkItem> context, CancellationToken ct) =>
        operations.ClaimAsync(context, ct);
}
