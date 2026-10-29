using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;
namespace Bdgrz.Compliance.Features.Workforce.Services;

public sealed class WorkRelationshipReadConsistency(IWorkRelationshipDirectoryReader directory,
    IAggregateReader reader, IDomainEventReader events)
{
    public async ValueTask<Result<WorkRelationshipView>> GetAsync(Uuid tenantId, Uuid relationshipId,
        long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<WorkRelationshipView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum work relationship revision must be positive."));
        var source = await reader.HydrateAsync(new WorkRelationship(tenantId, relationshipId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated)
            return Result<WorkRelationshipView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The work relationship was not found."));
        var view = await directory.GetAsync(tenantId, relationshipId, ct).ConfigureAwait(false);
        if (view is not null && view.TenantId == tenantId && view.RelationshipId == relationshipId &&
            view.Revision >= source.Revision &&
            (minimumRevision is null || view.Revision >= minimumRevision))
            return Result<WorkRelationshipView>.Success(view);
        return Result<WorkRelationshipView>.Failure(new RequestError(RequestErrorKind.Conflict,
            minimumRevision is { } minimum && source.Revision < minimum
                ? $"The work relationship source has not reached revision {minimum}."
                : "The work relationship projection has not reached the requested revision.",
            isTransient: true));
    }

    /// <summary>Checks the source area cursor before returning even an empty list.</summary>
    public async ValueTask<Result> EnsureListCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var fence = await CaptureListFenceAsync(tenantId, ct).ConfigureAwait(false);
        return fence.IsSuccess ? Result.Success : Result.Failure(fence.Error);
    }

    /// <summary>
    ///     Returns the projection checkpoint once it has reached the source, so a multi-page read can
    ///     later prove with <see cref="EnsureFenceHeldAsync" /> that nothing was projected meanwhile.
    /// </summary>
    public async ValueTask<Result<ProjectionCheckpoint>> CaptureListFenceAsync(Uuid tenantId,
        CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        await using var pending = events.ReadAsync(
            EventStreamPattern.ForPattern(tenantId.ToString(), "work-relationships"),
            checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result<ProjectionCheckpoint>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The work relationship list projection has not reached the source.", isTransient: true))
            : Result<ProjectionCheckpoint>.Success(checkpoint);
    }

    /// <summary>Fails transiently when the projection moved past a captured fence.</summary>
    public async ValueTask<Result> EnsureFenceHeldAsync(Uuid tenantId, ProjectionCheckpoint fence,
        CancellationToken ct)
    {
        var current = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        return current.Cursor.Value == fence.Cursor.Value
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The roster changed while it was being read.", isTransient: true));
    }
}
