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
    public ValueTask HandleAsync(TenantRegistered ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantSlugConfirmed ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantSlugRejected ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantSuspended ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantSuspendedV2 ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantReactivated ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantReactivatedV2 ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantOffboardingStarted ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantOffboardingStartedV2 ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantActivated ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantActivatedV2 ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TenantSlugChanged ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
