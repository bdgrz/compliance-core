using Cntryl.Portia;
using Bdgrz.Compliance.Features.Changes.Aggregates;
using Bdgrz.Compliance.Features.Changes.Events;
using Bdgrz.Compliance.Features.Changes.Projections;
namespace Bdgrz.Compliance.Features.Changes.Projectors;

public sealed partial class SignificantChangeDirectoryProjector(
    ISignificantChangeDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(SignificantChange.Area), ProjectorName),
      IProjectorHandler<SignificantChangeRecorded>,
      IProjectorHandler<SignificantChangeRevised>,
      IProjectorHandler<SignificantChangeImpactAssessed>,
      IProjectorHandler<ImpactReviewWorkLinked>,
      IProjectorHandler<SignificantChangeSystemDescriptionDecisionRecorded>
{
    public const string ProjectorName = "SignificantChangeDirectoryV2";

    public async ValueTask<Result> HandleAsync(IProjectorContext<SignificantChangeRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<SignificantChangeRevised> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<SignificantChangeImpactAssessed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ImpactReviewWorkLinked> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<SignificantChangeSystemDescriptionDecisionRecorded> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
