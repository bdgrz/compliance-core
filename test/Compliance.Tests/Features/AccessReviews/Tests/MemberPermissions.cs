using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

/// <summary>Grants every permission to the allowed users and none to anyone else.</summary>
sealed class MemberPermissions : IPermissionAuthorizer
{
    readonly HashSet<Uuid> _allowed = [];
    readonly HashSet<(Uuid UserId, string Permission)> _denied = [];
    readonly HashSet<Uuid> _restrictedReadAllowed = [];

    public void Allow(Uuid userId) => _allowed.Add(userId);
    public void Deny(Uuid userId, string permission) => _denied.Add((userId, permission));
    public void Restore(Uuid userId, string permission) => _denied.Remove((userId, permission));
    public void AllowRestrictedRead(Uuid userId) => _restrictedReadAllowed.Add(userId);
    public void DenyRestrictedRead(Uuid userId) => _restrictedReadAllowed.Remove(userId);

    public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
        string permission, CancellationToken ct = default) =>
        ValueTask.FromResult(permission == RbacPermissions.ApplicationRestrictedRead
            ? _restrictedReadAllowed.Contains(userId)
            : _allowed.Contains(userId) && !_denied.Contains((userId, permission)));
}
