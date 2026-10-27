using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Readers;
namespace Bdgrz.Compliance.Features.Work.Services;

/// <summary>Fences a queue read against lagging or changing required source projections.</summary>
public sealed class WorkQueueReadConsistency(IDomainEventReader events,
    IEnumerable<IAccountableWorkItemDirectoryReader>? accountableWorkItems = null)
{
    internal IDomainEventReader EventReader => events;
    readonly IAccountableWorkItemDirectoryReader[] _accountableWorkItems =
        accountableWorkItems?.OrderBy(static reader => reader.ProjectorName,
            StringComparer.Ordinal).ToArray() ?? [];

    public async ValueTask<Result<WorkQueueReadFence>> CaptureAsync(Uuid tenantId,
        CancellationToken ct)
    {
        var workItemProjections = new List<WorkItemProjectionFence>(_accountableWorkItems.Length);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var workItems in _accountableWorkItems)
        {
            if (!identities.Add(workItems.ProjectorName))
                throw new InvalidOperationException(
                    $"The work queue has duplicate projector identity {workItems.ProjectorName}.");
            var pattern = workItems.SourcePattern(tenantId);
            var checkpoint = await workItems.LoadCheckpointAsync(tenantId, ct)
                .ConfigureAwait(false);
            if (await HasPendingSourceAsync(pattern, checkpoint, ct).ConfigureAwait(false))
                return Result<WorkQueueReadFence>.Failure(BehindSourceError());
            workItemProjections.Add(new WorkItemProjectionFence(workItems.ProjectorName,
                checkpoint));
        }

        return Result<WorkQueueReadFence>.Success(new WorkQueueReadFence(workItemProjections));
    }

    public async ValueTask<Result> ConfirmUnchangedAndCaughtUpAsync(Uuid tenantId,
        WorkQueueReadFence fence, CancellationToken ct)
    {
        if (fence.WorkItemProjections.Count != _accountableWorkItems.Length)
            return Result.Failure(BehindSourceError());

        foreach (var workItems in _accountableWorkItems)
        {
            var projectionFence = fence.WorkItemProjections.SingleOrDefault(item =>
                StringComparer.Ordinal.Equals(item.ProjectorName, workItems.ProjectorName));
            if (projectionFence is null)
                return Result.Failure(BehindSourceError());
            var pattern = workItems.SourcePattern(tenantId);
            var checkpoint = await workItems.LoadCheckpointAsync(tenantId, ct)
                .ConfigureAwait(false);
            if (checkpoint != projectionFence.Checkpoint ||
                await HasPendingSourceAsync(pattern, checkpoint, ct).ConfigureAwait(false))
                return Result.Failure(BehindSourceError());
        }

        return Result.Success;
    }

    async ValueTask<bool> HasPendingSourceAsync(EventStreamPattern pattern,
        ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(pattern, checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static RequestError BehindSourceError() => new(RequestErrorKind.Conflict,
        "The work queue sources changed or a required projection is behind. Retry the query.",
        isTransient: true);
}
