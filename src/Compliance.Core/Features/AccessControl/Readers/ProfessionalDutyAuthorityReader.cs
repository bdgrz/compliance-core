using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Readers;

/// <summary>
///     Resolves professional authority only from explicit current duty evidence on the designation owner of the
///     subject, duty and scope, and the active canonical directory. Nothing is read from a firm-wide catalog.
/// </summary>
sealed class ProfessionalDutyAuthorityReader(IAggregateReader reader, FirmStaffViews staffViews,
    IPlatformUserDirectoryReader users, IFirmProfessionalDutyAssertionVerifier assertions,
    ProfessionalDutyReader dutyViews, TimeProvider clock)
{
    public async ValueTask<CurrentProfessionalDuty?> ReadCurrentAsync(Uuid userId, string duty,
        Uuid? tenantId, CancellationToken ct)
    {
        if (userId == Uuid.Empty || !ProfessionalDutyRules.IsSupportedDutyScope(duty, tenantId) ||
            !await users.ExistsAsync(userId, ct).ConfigureAwait(false))
            return null;
        if (await staffViews.FindByUserAsync(userId, ct).ConfigureAwait(false) is not { IsActive: true } staff)
            return null;
        var owner = await reader.HydrateAsync(new FirmProfessionalDutyDesignation(userId, duty, tenantId), ct)
            .ConfigureAwait(false);
        if (owner.LatestContent is null)
            return null;
        FirmProfessionalDutyDesignationView? designation;
        try
        {
            designation = await dutyViews.ReadCurrentAsync(owner, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        if (designation is null || !designation.IsActive || designation.AuthorityStatus != "active" || designation.UserId != userId ||
            designation.Duty != duty || designation.TenantId != tenantId || designation.DesignatedAt > now ||
            designation.NotBefore > now || designation.ExpiresAt <= now ||
            designation.StaffMemberId != staff.StaffMemberId || designation.DirectoryStaffRevision != staff.Revision)
            return null;
        if (!assertions.TryVerify(owner.LatestSignedAssertion, now, out var evidence) || evidence.Status != "active" ||
            evidence.AssertionId != designation.AssertionId || evidence.DesignationId != designation.DesignationId ||
            evidence.UserId != designation.UserId || evidence.Duty != designation.Duty ||
            evidence.TenantId != designation.TenantId || evidence.Issuer != designation.AuthorityIssuer ||
            evidence.AuthorityRevision != designation.AuthorityRevision ||
            evidence.SupersedesAssertionId != designation.SupersedesAssertionId ||
            evidence.IssuedAt != designation.AssertionIssuedAt || evidence.NotBefore != designation.NotBefore ||
            evidence.ExpiresAt != designation.ExpiresAt || evidence.EvidenceDigest != designation.EvidenceDigest)
            return null;
        return designation.ContinuityStartRevision is > 0 and var revision && revision <= designation.Revision
            ? new CurrentProfessionalDuty(staff, designation, revision)
            : null;
    }
}
