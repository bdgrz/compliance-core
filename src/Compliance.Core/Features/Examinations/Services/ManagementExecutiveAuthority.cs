using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Examinations.Services;

/// <summary>
/// Resolves CEO/CISO authority from current, program-scoped RBAC grants and the current direct
/// client membership. Token role claims and client-authored signer lists alone are not authority.
/// </summary>
public sealed class ManagementExecutiveAuthority(IAggregateReader reader,
    IAccessGrantPermissionAuthorizer permissions)
{
    static readonly (string Role, string Permission)[] ExecutiveRoles =
    [
        ("ceo", RbacPermissions.ManagementExecutiveCeo),
        ("ciso", RbacPermissions.ManagementExecutiveCiso),
    ];

    public ValueTask<string?> ResolveExecutiveRoleAsync(Uuid tenantId, Uuid programId,
        Uuid userId, Uuid memberId, CancellationToken ct) =>
        ResolveRoleAsync(tenantId, programId, userId, memberId, ExecutiveRoles, ct);

    public async ValueTask<string?> ResolveSignerRoleAsync(Uuid tenantId, Uuid programId,
        Uuid userId, Uuid memberId, IReadOnlyList<ManagementAssertionSigner> signers,
        CancellationToken ct)
    {
        if (signers is null)
            return null;

        var eligibleRoles = ExecutiveRoles.Where(item => signers.Any(signer =>
            signer.MemberId == memberId &&
            StringComparer.OrdinalIgnoreCase.Equals(signer.Role, item.Role))).ToArray();
        return await ResolveRoleAsync(tenantId, programId, userId, memberId, eligibleRoles, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<string?> ResolveRoleAsync(Uuid tenantId, Uuid programId, Uuid userId,
        Uuid memberId, (string Role, string Permission)[] eligibleRoles,
        CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || userId == Uuid.Empty ||
            memberId != RbacIds.Member(tenantId, userId) || eligibleRoles.Length == 0)
            return null;
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
            .ConfigureAwait(false);
        if (!member.IsRegistered || member.IsSuspended || member.IsDeprovisioned ||
            member.Affiliation != "client_personnel" || member.UserId != userId)
            return null;

        var authorizedRoles = new List<string>(eligibleRoles.Length);
        foreach (var (role, permission) in eligibleRoles)
        {
            if (await permissions.IsAllowedAsync(tenantId, userId, memberId, programId,
                    permission, ct).ConfigureAwait(false))
                authorizedRoles.Add(role);
        }

        return authorizedRoles.Count == 1 ? authorizedRoles[0] : null;
    }
}
