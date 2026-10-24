using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Checks the existing accepted assignment path for advisory-note reads.</summary>
sealed class ProfessionalAdvisoryReadAccess(IAggregateReader reader, FirmStaffViews staffViews, ITenantActivity tenants,
    IProgramResourceScopeResolver programScopes, TimeProvider clock,
    ITenantMembershipDirectoryReader memberships,
    IAccessGrantPermissionAuthorizer scopedPermissions)
    : IAdvisorSharedSourceAuthorization, IAdvisorHandoffListAccess
{
    public async ValueTask<bool> CanReadAsync(Uuid tenantId, Uuid programId, Uuid userId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || userId == Uuid.Empty ||
            !await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false) ||
            !await programScopes.IsTenantProgramAsync(tenantId, programId, ct).ConfigureAwait(false))
            return false;

        if (await staffViews.FindByUserAsync(userId, ct).ConfigureAwait(false) is not { IsActive: true, Practice: "advisory" } staff ||
            staff.StaffMemberId == Uuid.Empty || staff.Revision <= 0)
            return false;

        var ledger = await reader.HydrateAsync(new IndependenceLedger(tenantId), ct).ConfigureAwait(false);
        var effectiveAt = clock.GetUtcNow();
        foreach (var engagement in ledger.Engagements)
        {
            if (engagement.TenantId != tenantId || engagement.Content.Practice != "advisory" ||
                engagement.Status != "accepted" ||
                ledger.Acceptance(engagement.EngagementId) is not { Status: "active" } acceptance ||
                acceptance.TenantId != tenantId || !acceptance.Rules.IsRatified ||
                !ledger.IsEligibleForProfessionalAccess(engagement.EngagementId, staff.StaffMemberId,
                userId, currentRuleVersion: acceptance.Rules.Version,
                currentDirectoryStaffRevision: staff.Revision, effectiveAt: effectiveAt))
                continue;

            if (await MatchesProgramScopeAsync(tenantId, programId, acceptance, ct).ConfigureAwait(false))
                return true;
        }

        return false;
    }

    public async ValueTask<AdvisorHandoffListVisibility> GetListVisibilityAsync(Uuid tenantId,
        Uuid programId, Uuid userId, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || userId == Uuid.Empty ||
            !await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false) ||
            !await programScopes.IsTenantProgramAsync(tenantId, programId, ct)
                .ConfigureAwait(false))
            return AdvisorHandoffListVisibility.Denied;

        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is { Affiliation: "client_personnel" } &&
            membership.TenantId == tenantId && membership.UserId == userId &&
            membership.IsActiveOn(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)) &&
            await scopedPermissions.IsAllowedAsync(tenantId, userId,
                RbacIds.Member(tenantId, userId), programId, IProgramReadRequest.ReadPermission, ct)
                .ConfigureAwait(false))
            return AdvisorHandoffListVisibility.Full;

        return await CanReadAsync(tenantId, programId, userId, ct).ConfigureAwait(false) ||
               await CanReadGuestProgramAsync(tenantId, programId, userId, ct)
                   .ConfigureAwait(false)
            ? AdvisorHandoffListVisibility.SharedOnly
            : AdvisorHandoffListVisibility.Denied;
    }

    /// <summary>Draft selections become visible to an assigned Advisor only while actively shared.</summary>
    public async ValueTask<bool> CanReadHandoffAsync(Uuid tenantId, Uuid programId,
        Uuid handoffId, Uuid userId, CancellationToken ct = default)
    {
        if (handoffId == Uuid.Empty)
            return false;
        var assignedAdvisor = await CanReadAsync(tenantId, programId, userId, ct)
            .ConfigureAwait(false);
        var scopedGuest = !assignedAdvisor && await CanReadGuestProgramAsync(tenantId,
            programId, userId, ct).ConfigureAwait(false);
        if (!assignedAdvisor && !scopedGuest)
            return false;

        var handoff = await reader.HydrateAsync(new AdvisorHandoff(tenantId, programId,
            handoffId), ct).ConfigureAwait(false);
        return IsSharedHandoff(handoff, programId);
    }

    /// <summary>Allows source content reads only for an exact selection in an open handoff.</summary>
    public async ValueTask<bool> CanReadSelectedSourceAsync(Uuid tenantId, Uuid programId,
        Uuid handoffId, string sourceFamily, Uuid recordId, Uuid? entryId, Uuid userId,
        CancellationToken ct = default)
    {
        if (!await CanReadHandoffAsync(tenantId, programId, handoffId, userId, ct)
                .ConfigureAwait(false))
            return false;
        var handoff = await reader.HydrateAsync(new AdvisorHandoff(tenantId, programId,
            handoffId), ct).ConfigureAwait(false);
        return handoff.Selection.Any(source => source.SourceFamily == sourceFamily &&
            source.RecordId == recordId && source.EntryId == entryId);
    }

    public async ValueTask<bool> CanReadSelectedSourceAsync(Uuid tenantId, Uuid programId,
        Uuid handoffId, AdvisorRecordVersion sourceVersion, Uuid userId,
        CancellationToken ct = default)
    {
        if (sourceVersion is null || !sourceVersion.HasValidIdentity ||
            !await CanReadHandoffAsync(tenantId, programId, handoffId, userId, ct)
                .ConfigureAwait(false))
            return false;
        var handoff = await reader.HydrateAsync(new AdvisorHandoff(tenantId, programId,
            handoffId), ct).ConfigureAwait(false);
        return handoff.Selection.Contains(sourceVersion);
    }

    /// <summary>An assigned Advisor may correct only their own feedback while it remains shared.</summary>
    public async ValueTask<bool> CanCorrectFeedbackAsync(Uuid tenantId, Uuid programId,
        Uuid handoffId, Uuid feedbackId, Uuid userId, CancellationToken ct = default)
    {
        if (handoffId == Uuid.Empty || feedbackId == Uuid.Empty ||
            !await CanReadHandoffAsync(tenantId, programId, handoffId, userId, ct)
                .ConfigureAwait(false))
            return false;

        var handoff = await reader.HydrateAsync(new AdvisorHandoff(tenantId, programId,
            handoffId), ct).ConfigureAwait(false);
        return IsSharedHandoff(handoff, programId) && handoff.ReadFeedback().Any(feedback =>
            feedback.FeedbackId == feedbackId && feedback.Author.Kind == "member" &&
            feedback.Author.Id == RbacIds.Member(tenantId, userId).ToString());
    }

    /// <summary>An assigned Advisor may post only against an exact version in a shared handoff.</summary>
    public async ValueTask<bool> CanRecordFeedbackAsync(Uuid tenantId, Uuid programId,
        Uuid handoffId, AdvisorRecordVersion sourceVersion, Uuid userId,
        CancellationToken ct = default)
    {
        if (handoffId == Uuid.Empty || sourceVersion is null || !sourceVersion.HasValidIdentity ||
            !await CanReadHandoffAsync(tenantId, programId, handoffId, userId, ct)
                .ConfigureAwait(false))
            return false;

        var handoff = await reader.HydrateAsync(new AdvisorHandoff(tenantId, programId,
            handoffId), ct).ConfigureAwait(false);
        return IsSharedHandoff(handoff, programId) &&
               handoff.Selection.Contains(sourceVersion);
    }

    bool IsSharedHandoff(AdvisorHandoff handoff, Uuid programId) => handoff.IsCreated &&
        handoff.ProgramId == programId && handoff.Status == "ready_for_review" &&
        handoff.ExpiresOn >= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    async ValueTask<bool> CanReadGuestProgramAsync(Uuid tenantId, Uuid programId,
        Uuid userId, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || userId == Uuid.Empty ||
            !await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false) ||
            !await programScopes.IsTenantProgramAsync(tenantId, programId, ct)
                .ConfigureAwait(false))
            return false;

        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is not { Affiliation: "guest", IsSuspended: false, IsDeprovisioned: false } ||
            membership.TenantId != tenantId || membership.UserId != userId)
            return false;

        var memberId = RbacIds.Member(tenantId, userId);
        return await scopedPermissions.IsAllowedAsync(tenantId, userId, memberId, programId,
            RbacPermissions.TenantAccess, ct).ConfigureAwait(false);
    }

    async ValueTask<bool> MatchesProgramScopeAsync(Uuid tenantId, Uuid programId,
        ServiceEngagementAcceptanceView acceptance, CancellationToken ct)
    {
        if (acceptance.BoundaryId is null && acceptance.BoundaryVersionId is null &&
            acceptance.BoundaryRevision is null && acceptance.BoundaryApprovalDecisionId is null)
            return true;

        if (acceptance.BoundaryId is not { } boundaryId || boundaryId == Uuid.Empty ||
            acceptance.BoundaryVersionId is not { } versionId || versionId == Uuid.Empty ||
            acceptance.BoundaryRevision is not > 0 ||
            acceptance.BoundaryApprovalDecisionId is not { } decisionId || decisionId == Uuid.Empty)
            return false;

        var boundary = await reader.HydrateAsync(new SystemBoundary(tenantId, boundaryId), ct)
            .ConfigureAwait(false);
        return boundary.IsCreated && boundary.ProgramId == programId &&
            boundary.IsVersionApproved(versionId, acceptance.BoundaryRevision.Value, decisionId);
    }
}
