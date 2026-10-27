using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

sealed class ReleaseArtifactLegalHoldHandler(ArtifactRetentionMutation mutation) : IRequestHandler<ReleaseArtifactLegalHold>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReleaseArtifactLegalHold> context, CancellationToken ct) =>
        mutation.ReleaseAsync(context, ct);
}
