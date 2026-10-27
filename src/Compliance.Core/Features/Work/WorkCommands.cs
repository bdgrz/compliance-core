using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Resolves the one work item a command targets and records its accountability change.</summary>
static class WorkCommands
{
    /// <summary>Runs <paramref name="command" /> against the visible item, or returns not found.</summary>
    public static async ValueTask<Result<WorkItemDetailView>> RunAsync<TRequest>(
        WorkQueueReader queue, IRequestContext<TRequest> context, Uuid tenantId, Uuid programId,
        Uuid workItemId,
        Func<OperationsActor, WorkQueueSnapshot, WorkQueueEntry,
            ValueTask<Result<WorkItemDetailView>>> command, CancellationToken ct)
        where TRequest : IRequestBase
    {
        var actor = OperationsActor.From(context.Actor, tenantId);
        var found = await queue.FindAsync(tenantId, programId, actor, workItemId, ct)
            .ConfigureAwait(false);
        if (!found.IsSuccess)
            return Result<WorkItemDetailView>.Failure(found.Error!);
        var (snapshot, entry) = found.Value;
        return await command(actor, snapshot, entry).ConfigureAwait(false);
    }

    public static Result<WorkItemDetailView> Fail(RequestErrorKind kind, string message) =>
        Result<WorkItemDetailView>.Failure(new RequestError(kind, message));
}
