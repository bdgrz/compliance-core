using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Handlers;

/// <summary>Records one authorized accountability operation and preserves its exact original native result.</summary>
public sealed class DelegateWorkItemHandler(WorkAssignmentOperations operations) : IRequestHandler<DelegateWorkItem, WorkItemDetailView>
{
    public ValueTask<Result<WorkItemDetailView>> HandleAsync(IRequestContext<DelegateWorkItem> context, CancellationToken ct) =>
        operations.DelegateAsync(context, ct);
}
