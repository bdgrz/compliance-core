using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

sealed class PlaceArtifactLegalHoldHandler(ArtifactRetentionMutation mutation) : IRequestHandler<PlaceArtifactLegalHold>
{
    public ValueTask<Result> HandleAsync(IRequestContext<PlaceArtifactLegalHold> context, CancellationToken ct) =>
        mutation.PlaceAsync(context, ct);
}
