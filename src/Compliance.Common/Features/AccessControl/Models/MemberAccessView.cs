using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Models;

public sealed record MemberAccessView(Uuid TenantId, Uuid UserId, Uuid MemberId,
    IReadOnlyList<MemberAccessPath> Paths, IReadOnlyList<MemberAccessGrantPath> GrantPaths,
    IReadOnlyList<string> EffectivePermissions);
