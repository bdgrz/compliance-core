using Cntryl.Portia;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Services;

public sealed class MethodologyTemplateDirectoryReadConsistency(IMethodologyTemplateDirectoryReader directory,
    IDomainEventReader events)
{
    public async ValueTask<Result<ProjectionCheckpoint>> CaptureAsync(CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(ct).ConfigureAwait(false);
        return await HasPendingAsync(checkpoint, ct).ConfigureAwait(false)
            ? Result<ProjectionCheckpoint>.Failure(Behind()) : Result<ProjectionCheckpoint>.Success(checkpoint);
    }

    public async ValueTask<Result> ConfirmAsync(ProjectionCheckpoint fence, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(ct).ConfigureAwait(false);
        return checkpoint != fence || await HasPendingAsync(checkpoint, ct).ConfigureAwait(false)
            ? Result.Failure(Behind()) : Result.Success;
    }

    async ValueTask<bool> HasPendingAsync(ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(FitzMethodologyTemplateDirectory.Pattern, checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }
    static RequestError Behind() => new(RequestErrorKind.Conflict,
        "The published methodology directory changed or has not reached its source. Retry the query.", isTransient: true);
}
