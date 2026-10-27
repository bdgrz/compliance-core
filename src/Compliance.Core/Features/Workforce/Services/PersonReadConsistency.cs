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
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
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
using Bdgrz.Compliance.Features.Workforce.Projections;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;
namespace Bdgrz.Compliance.Features.Workforce.Services;

public sealed class PersonReadConsistency(IPersonDirectoryReader directory,
    IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async ValueTask<Result<PersonView>> GetAsync(Uuid tenantId, Uuid personId,
        long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<PersonView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum person revision must be positive."));
        PersonReadSnapshot snapshot;
        try
        {
            snapshot = await new PersonReader(reader, events, content).ReadAsync(tenantId, personId, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<PersonView>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact person source values do not verify: {error.Message}", error is ImmutableContentUnavailableException));
        }
        var source = snapshot.Owner;
        if (!source.IsCreated)
            return Result<PersonView>.Failure(new RequestError(RequestErrorKind.NotFound, "The person was not found."));
        var view = await directory.GetAsync(tenantId, personId, ct).ConfigureAwait(false);
        if (view is not null && view.TenantId == tenantId && view.PersonId == personId &&
            view.Revision >= source.Revision &&
            (minimumRevision is null || view.Revision >= minimumRevision && source.Revision >= minimumRevision))
            return Result<PersonView>.Success(snapshot.View!);
        return Result<PersonView>.Failure(new RequestError(RequestErrorKind.Conflict,
            minimumRevision is { } minimum && source.Revision < minimum
                ? $"The person source has not reached revision {minimum}."
                : "The person projection has not reached the requested revision.",
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
            EventStreamPattern.ForPattern(tenantId.ToString(), "people"),
            checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result<ProjectionCheckpoint>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The person list projection has not reached the source.", isTransient: true))
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
