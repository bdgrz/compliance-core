using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Features.Retention.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Handlers;

sealed class ReleaseArtifactLegalHoldHandler(ArtifactRetentionMutation mutation) : IRequestHandler<ReleaseArtifactLegalHold>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReleaseArtifactLegalHold> context, CancellationToken ct) =>
        mutation.ReleaseAsync(context, ct);
}
