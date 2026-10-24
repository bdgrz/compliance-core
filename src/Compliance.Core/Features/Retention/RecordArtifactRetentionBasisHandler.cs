using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

sealed class RecordArtifactRetentionBasisHandler(ArtifactRetentionMutation mutation) : IRequestHandler<RecordArtifactRetentionBasis>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordArtifactRetentionBasis> context, CancellationToken ct) =>
        mutation.RecordBasisAsync(context, ct);
}
