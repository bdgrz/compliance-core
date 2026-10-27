using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Features.Retention.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Handlers;

sealed class RecordArtifactRetentionBasisHandler(ArtifactRetentionMutation mutation) : IRequestHandler<RecordArtifactRetentionBasis>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordArtifactRetentionBasis> context, CancellationToken ct) =>
        mutation.RecordBasisAsync(context, ct);
}
