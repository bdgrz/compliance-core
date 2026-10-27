using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Tenants.Projections;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Projectors;

public sealed partial class TenantMembershipProjector(ITenantMembershipDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("rbac-members"), "TenantMembership"),
      IProjectorHandler<MemberRegistered>,
      IProjectorHandler<MemberSuspended>,
      IProjectorHandler<MemberReinstated>,
      IProjectorHandler<MemberDeprovisioned>
{
    public ValueTask HandleAsync(MemberRegistered ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(MemberSuspended ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(MemberReinstated ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(MemberDeprovisioned ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
