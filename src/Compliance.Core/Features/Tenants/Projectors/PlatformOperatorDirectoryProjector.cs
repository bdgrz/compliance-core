using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Tenants.Projections;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Projectors;

public sealed partial class PlatformOperatorDirectoryProjector(IPlatformOperatorDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForPattern("bdgrz", "platform-operators"), "PlatformOperatorDirectory"),
      IProjectorHandler<PlatformOperatorBootstrapGrantAndMarkAccepted>,
      IProjectorHandler<PlatformOperatorBootstrapAccessGranted>,
      IProjectorHandler<PlatformOperatorAccessGranted>,
      IProjectorHandler<PlatformOperatorAccessRevoked>,
      IProjectorHandler<PlatformOperatorPrimaryMarkAccepted>,
      IProjectorHandler<PlatformOperatorPrimaryUnmarked>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<PlatformOperatorBootstrapGrantAndMarkAccepted> context, CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PlatformOperatorBootstrapAccessGranted> context, CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PlatformOperatorAccessGranted> context, CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PlatformOperatorAccessRevoked> context, CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PlatformOperatorPrimaryMarkAccepted> context, CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<PlatformOperatorPrimaryUnmarked> context, CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }

}
