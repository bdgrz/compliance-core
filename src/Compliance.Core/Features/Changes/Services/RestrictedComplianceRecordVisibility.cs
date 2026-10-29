using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
namespace Bdgrz.Compliance.Features.Changes.Services;

/// <summary>Enforces the internal visibility rule for restricted change and incident records.</summary>
public sealed class RestrictedComplianceRecordVisibility(IPermissionAuthorizer permissions)
{
    static readonly HashSet<string> HandlingClasses = new(StringComparer.Ordinal)
        { "public", "internal", "confidential", "restricted" };

    public async ValueTask<bool> CanReadAsync(Uuid tenantId, ClaimsPrincipal actor,
        string? handlingClass, IReadOnlyCollection<Uuid> assessorMemberIds,
        CancellationToken ct = default) => await CanAccessAsync(tenantId, actor, handlingClass,
            assessorMemberIds, RbacPermissions.ComplianceRecordRestrictedRead, ct).ConfigureAwait(false);

    public async ValueTask<bool> CanEditAsync(Uuid tenantId, ClaimsPrincipal actor,
        string? handlingClass, IReadOnlyCollection<Uuid> assessorMemberIds,
        CancellationToken ct = default) => await CanAccessAsync(tenantId, actor, handlingClass,
            assessorMemberIds, RbacPermissions.ComplianceRecordRestrictedWrite, ct).ConfigureAwait(false);

    async ValueTask<bool> CanAccessAsync(Uuid tenantId, ClaimsPrincipal actor,
        string? handlingClass, IReadOnlyCollection<Uuid> assessorMemberIds, string permission,
        CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || !HandlingClasses.Contains(handlingClass ?? ""))
            return false;
        if (handlingClass != "restricted")
            return true;
        if (!UserIdentityClaims.TryGetBdgrzSubject(actor, out var userId) || assessorMemberIds is null)
            return false;

        var memberId = RbacIds.Member(tenantId, userId);
        return assessorMemberIds.Contains(memberId) ||
            await permissions.IsAllowedAsync(tenantId, userId, memberId,
                permission, ct).ConfigureAwait(false);
    }
}
