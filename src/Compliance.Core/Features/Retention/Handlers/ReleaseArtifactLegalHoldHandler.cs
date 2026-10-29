using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Retention.Services;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Retention.Handlers;

sealed class ReleaseArtifactLegalHoldHandler(ArtifactRetentionMutation mutation) : IRequestHandler<ReleaseArtifactLegalHold>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReleaseArtifactLegalHold> context, CancellationToken ct) =>
        mutation.ReleaseAsync(context, ct);
}
