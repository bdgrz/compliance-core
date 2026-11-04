using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Projectors;

public sealed partial class PermissionProjector(IPermissionProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "PermissionProjection"),
      IProjectorHandler<MemberRegistered>,
      IProjectorHandler<MemberSuspended>,
      IProjectorHandler<MemberReinstated>,
      IProjectorHandler<MemberDeprovisioned>,
      IProjectorHandler<TeamDefined>,
      IProjectorHandler<TeamDeleted>,
      IProjectorHandler<TeamMemberAssigned>,
      IProjectorHandler<TeamMemberRemoved>,
      IProjectorHandler<RoleDefined>,
      IProjectorHandler<RoleRenamed>,
      IProjectorHandler<RoleDeleted>,
      IProjectorHandler<RolePermissionAssigned>,
      IProjectorHandler<RolePermissionRemoved>,
      IProjectorHandler<TeamRoleAssigned>,
      IProjectorHandler<TeamRoleRemoved>
{
    public static bool RevokesAllMemberAccess(DomainEvent domainEvent, Uuid memberId) => domainEvent switch
    {
        MemberRegistered registered => registered.MemberId == memberId &&
            !string.Equals(registered.Affiliation, "client_personnel", StringComparison.Ordinal),
        MemberSuspended suspended => suspended.MemberId == memberId,
        MemberDeprovisioned deprovisioned => deprovisioned.MemberId == memberId,
        _ => false,
    };

    public static bool RevokesAccessPath(DomainEvent domainEvent, Uuid memberId,
        string permission, MemberAccessEdge currentAccess) => domainEvent switch
        {
            TeamMemberRemoved removed => removed.MemberId == memberId &&
                removed.TeamId == currentAccess.TeamId,
            TeamDeleted deleted => currentAccess.TeamId == deleted.TeamId,
            TeamRoleRemoved removed => currentAccess.TeamId == removed.TeamId &&
                currentAccess.RoleId == removed.RoleId,
            RoleDeleted deleted => currentAccess.RoleId == deleted.RoleId,
            RolePermissionRemoved removed => currentAccess.RoleId == removed.RoleId &&
                currentAccess.Permissions.Contains(permission, StringComparer.Ordinal),
            _ => false,
        };

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

    public async ValueTask<Result> HandleAsync(IProjectorContext<TeamDefined> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TeamDeleted> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TeamMemberAssigned> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TeamMemberRemoved> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<RoleDefined> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<RoleRenamed> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<RoleDeleted> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<RolePermissionAssigned> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<RolePermissionRemoved> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TeamRoleAssigned> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TeamRoleRemoved> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
