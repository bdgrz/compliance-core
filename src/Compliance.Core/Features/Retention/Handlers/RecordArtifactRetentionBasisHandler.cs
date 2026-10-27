using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Retention.Services;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Retention.Handlers;

sealed class RecordArtifactRetentionBasisHandler(ArtifactRetentionMutation mutation) : IRequestHandler<RecordArtifactRetentionBasis>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordArtifactRetentionBasis> context, CancellationToken ct) =>
        mutation.RecordBasisAsync(context, ct);
}
