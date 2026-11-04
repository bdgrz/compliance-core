using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Projections;
namespace Bdgrz.Compliance.Features.Tenants.Projectors;

public sealed partial class TenantDirectoryProjector(ITenantDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForPattern("bdgrz", "tenants"), "TenantDirectory"),
      IProjectorHandler<TenantRegistered>,
      IProjectorHandler<TenantSlugConfirmed>,
      IProjectorHandler<TenantSlugRejected>,
      IProjectorHandler<TenantSuspended>,
      IProjectorHandler<TenantSuspendedV2>,
      IProjectorHandler<TenantReactivated>,
      IProjectorHandler<TenantReactivatedV2>,
      IProjectorHandler<TenantOffboardingStarted>,
      IProjectorHandler<TenantOffboardingStartedV2>,
      IProjectorHandler<TenantActivated>,
      IProjectorHandler<TenantActivatedV2>,
      IProjectorHandler<TenantSlugChanged>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<TenantRegistered> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TenantSlugConfirmed> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TenantSlugRejected> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TenantSuspended> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TenantSuspendedV2> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TenantReactivated> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TenantReactivatedV2> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TenantOffboardingStarted> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TenantOffboardingStartedV2> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TenantActivated> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TenantActivatedV2> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TenantSlugChanged> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
