using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Retention.Services;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Retention.Handlers;

sealed class PlaceArtifactLegalHoldHandler(ArtifactRetentionMutation mutation) : IRequestHandler<PlaceArtifactLegalHold>
{
    public ValueTask<Result> HandleAsync(IRequestContext<PlaceArtifactLegalHold> context, CancellationToken ct) =>
        mutation.PlaceAsync(context, ct);
}
