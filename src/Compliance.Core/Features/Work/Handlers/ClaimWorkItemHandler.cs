using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Handlers;

/// <summary>Records one authorized accountability operation and preserves its exact original native result.</summary>
public sealed class ClaimWorkItemHandler(WorkAssignmentOperations operations) : IRequestHandler<ClaimWorkItem, WorkItemDetailView>
{
    public ValueTask<Result<WorkItemDetailView>> HandleAsync(IRequestContext<ClaimWorkItem> context, CancellationToken ct) =>
        operations.ClaimAsync(context, ct);
}
