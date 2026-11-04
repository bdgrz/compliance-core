using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Tenants.Projections;
namespace Bdgrz.Compliance.Features.Tenants.Projectors;

public sealed partial class TenantMembershipProjector(ITenantMembershipDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("rbac-members"), "TenantMembership"),
      IProjectorHandler<MemberRegistered>,
      IProjectorHandler<MemberSuspended>,
      IProjectorHandler<MemberReinstated>,
      IProjectorHandler<MemberDeprovisioned>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<MemberRegistered> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<MemberSuspended> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<MemberReinstated> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<MemberDeprovisioned> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
