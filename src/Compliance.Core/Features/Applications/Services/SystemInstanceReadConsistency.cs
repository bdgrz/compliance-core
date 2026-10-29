using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Applications.Services;
namespace Bdgrz.Compliance.Features.Applications.Services;

public sealed class SystemInstanceReadConsistency(IApplicationDirectoryReader directory,
    IAggregateReader reader, LegacySystemInstanceSource legacy,
    IDomainEventReader events)
{
    public async ValueTask<Result<ProjectionCheckpoint>> CaptureApplicationListAsync(
        Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        return await HasPendingApplicationDirectorySourceAsync(tenantId, checkpoint, ct)
                .ConfigureAwait(false)
            ? Result<ProjectionCheckpoint>.Failure(ApplicationListBehindSourceError())
            : Result<ProjectionCheckpoint>.Success(checkpoint);
    }

    public async ValueTask<Result> ConfirmApplicationListUnchangedAndCaughtUpAsync(
        Uuid tenantId, ProjectionCheckpoint fence, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct)
            .ConfigureAwait(false);
        if (checkpoint != fence || await HasPendingApplicationDirectorySourceAsync(
                tenantId, checkpoint, ct).ConfigureAwait(false))
            return Result.Failure(ApplicationListBehindSourceError());
        return Result.Success;
    }

    public async ValueTask<Result> EnsureAsync(Uuid tenantId, Uuid applicationId,
        long? minimumApplicationRevision, Uuid? systemInstanceId,
        long? minimumInstanceRevision, CancellationToken ct)
    {
        if (minimumApplicationRevision is < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum application revision must be positive."));
        if (minimumInstanceRevision is < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum system instance revision must be positive."));
        var source = await reader.HydrateApplicationAsync(tenantId, applicationId, ct).ConfigureAwait(false);
        if (!source.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The application was not found."));
        if (minimumApplicationRevision is { } minimum && source.Revision < minimum)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The application source has not reached revision {minimum}.",
                isTransient: true));
        var projection = await directory.GetAsync(tenantId, applicationId, ct)
            .ConfigureAwait(false);
        if (projection is null || projection.Revision < source.Revision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The application projection has not reached the source revision.",
                isTransient: true));
        if (systemInstanceId is { } instanceId)
        {
            var instance = await reader.HydrateAsync(new DeclaredSystemInstance(tenantId,
                instanceId), ct).ConfigureAwait(false);
            long instanceRevision;
            if (instance.IsCreated)
            {
                if (instance.ApplicationId != applicationId)
                    return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                        "The system instance was not found."));
                instanceRevision = instance.Revision;
            }
            else
            {
                if (!await legacy.ExistsAsync(tenantId, applicationId, instanceId, ct)
                        .ConfigureAwait(false))
                    return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                        "The system instance was not found."));
                instanceRevision = 1;
            }
            if (minimumInstanceRevision is { } minimumInstance &&
                instanceRevision < minimumInstance)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The system instance source has not reached revision {minimumInstance}.",
                    isTransient: true));
            var projected = await directory.GetInstanceAsync(tenantId, instanceId, ct)
                .ConfigureAwait(false);
            if (projected is null || projected.Revision < instanceRevision)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The system instance projection has not reached the source revision.",
                    isTransient: true));
            if (projected.ApplicationId != applicationId)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The system instance was not found."));
        }
        else
        {
            // A list has no single instance stream to inspect. The versioned projector
            // processes old application and new instance events in one tenant cursor.
            var backlog = await ApplicationDirectoryBacklog.FindAsync(directory, events,
                tenantId, static ev => ev is ApplicationDeclared or ApplicationRevised or
                    SystemInstanceDeclared or SystemInstanceRegistered or SystemInstanceRevised or
                    ApplicationImportCommitted, ct).ConfigureAwait(false);
            if (!backlog.Clear)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The system instance list projection has not reached the source.",
                    isTransient: true));
        }
        return Result.Success;
    }

    async ValueTask<bool> HasPendingApplicationDirectorySourceAsync(Uuid tenantId,
        ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        var scanned = 0;
        await foreach (var pending in events.ReadAsync(
                           EventStreamPattern.ForPattern(tenantId.ToString()),
                           checkpoint.Cursor, ct).ConfigureAwait(false))
        {
            if (scanned++ == ApplicationDirectoryBacklog.ScanLimit)
                return true;
            if (pending.Event is ApplicationDeclared or ApplicationRevised or ApplicationRetired or
                SystemInstanceDeclared or SystemInstanceRegistered or SystemInstanceRevised or
                SystemInstanceRetired or
                ApplicationImportCommitted)
                return true;
        }
        return false;
    }

    static RequestError ApplicationListBehindSourceError() => new(RequestErrorKind.Conflict,
        "The system instance list projection has not reached a stable source checkpoint.",
        isTransient: true);
}
