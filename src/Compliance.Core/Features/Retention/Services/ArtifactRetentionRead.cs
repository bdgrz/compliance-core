using Cntryl.Portia;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Retention.Readers;
namespace Bdgrz.Compliance.Features.Retention.Services;

sealed class ArtifactRetentionRead(IAggregateReader reader, IDomainEventReader events, TimeProvider clock)
{
    public async ValueTask<Result<ArtifactRetentionView>> GetAsync(Uuid tenant, string kind, Uuid id,
        long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<ArtifactRetentionView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum retention revision must be positive."));
        var source = await ArtifactRetentionSourceReader.LoadAsync(reader, tenant, kind, id, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<ArtifactRetentionView>.Failure(source.Error!);
        var directory = new ArtifactRetentionReader(reader, events);
        var retention = await directory.ReadAsync(source.Value, ct).ConfigureAwait(false);
        if (!retention.IsSuccess)
            return Result<ArtifactRetentionView>.Failure(retention.Error!);
        if (minimumRevision > retention.Value.Revision)
            return Result<ArtifactRetentionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The requested retention composition revision is not yet durable.", true));
        var fence = await ArtifactRetentionSourceReader.CheckAsync(reader, source.Value, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<ArtifactRetentionView>.Failure(fence.Error!);
        var current = await directory.ReadAsync(source.Value, ct).ConfigureAwait(false);
        if (!current.IsSuccess || current.Value.Stamp != retention.Value.Stamp)
            return Result<ArtifactRetentionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Retention changed during this request.", true));
        return Result<ArtifactRetentionView>.Success(retention.Value.Assess(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)));
    }
}
