using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Features.Retention.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Handlers;

sealed class PlaceArtifactLegalHoldHandler(ArtifactRetentionMutation mutation) : IRequestHandler<PlaceArtifactLegalHold>
{
    public ValueTask<Result> HandleAsync(IRequestContext<PlaceArtifactLegalHold> context, CancellationToken ct) =>
        mutation.PlaceAsync(context, ct);
}
