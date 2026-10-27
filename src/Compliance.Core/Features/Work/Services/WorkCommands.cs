using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Projections;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Readers;
namespace Bdgrz.Compliance.Features.Work.Services;

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
