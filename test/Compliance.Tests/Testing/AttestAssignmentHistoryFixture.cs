using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Synthetic internal authority fixtures only; never evidence of real professional designation or ratification.
///     The permanent actual-assignment wall is read from the practice commitment owners, so the fixture seeds the
///     accepted engagement on its own owner and commits the staff member and the canonical user to the engagement's
///     practice, exactly as the acceptance process leaves them. The walls stay whether or not the engagement is later
///     closed; closure only closes the engagement.
/// </summary>
static class AttestAssignmentHistoryFixture
{
    static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    public static async Task SeedAsync(IServiceProvider provider, Uuid tenant, Uuid canonicalUserId,
        bool revoked = false, string practice = "attest") =>
        _ = await SeedAndReturnAsync(provider, tenant, canonicalUserId, revoked, practice);

    public static async Task<FirmStaffMemberView> SeedAndReturnAsync(IServiceProvider provider, Uuid tenant,
        Uuid canonicalUserId, bool revoked = false, string practice = "attest",
        BoundaryVersionView? boundary = null, BoundaryDecisionView? boundaryApproval = null) =>
        (await SeedAcceptedAsync(provider, tenant, canonicalUserId, revoked, practice, boundary, boundaryApproval)).Staff;

    /// <summary>Seeds the accepted engagement and returns its assignee together with the engagement identity.</summary>
    public static async Task<(FirmStaffMemberView Staff, Uuid EngagementId)> SeedAcceptedAsync(
        IServiceProvider provider, Uuid tenant, Uuid canonicalUserId, bool revoked = false, string practice = "attest",
        BoundaryVersionView? boundary = null, BoundaryDecisionView? boundaryApproval = null)
    {
        var staff = new FirmStaffMemberView(Uuid.CreateVersion4(), canonicalUserId, practice, "Synthetic assignee", true,
            1, ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), Now);
        var partnerUser = Uuid.CreateVersion4();
        var partnerActor = ActorReference.ForFirmStaff(partnerUser, "Synthetic partner");
        var rules = new IndependenceRuleVersionView(1, new IndependenceRuleContent(12,
            [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
            "Synthetic ratified test rules ONLY"), staff.Actor, Now, true);
        var engagementId = Uuid.CreateVersion4();
        var client = ActorReference.ForMember(RbacIds.Member(tenant, Uuid.CreateVersion4()),
            "Synthetic client administrator");
        var draft = await ServiceEngagementFacts.CreateAsync(provider, tenant, engagementId, new ServiceEngagementDraftContent(
            practice, "Synthetic scope", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), staff.StaffMemberId,
            boundary is null ? null : new ServiceEngagementBoundaryReference(boundary.BoundaryId, boundary.VersionId,
                boundary.Revision)), staff, client, Now);
        var acceptance = new ServiceEngagementAcceptanceView(tenant, engagementId, 1, 1,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), partnerUser, 1, 1, "Synthetic verified authority ONLY", null,
            Uuid.CreateVersion4(), boundary?.BoundaryId, boundary?.VersionId, boundary?.Revision,
            boundaryApproval?.DecisionId, new EngagementAcceptanceSourceTimes(Now, Now,
                boundary is null ? null : Now, boundaryApproval is null ? null : Now), rules, [], "allowed",
            "not_applicable", [],
            [new EngagementActualAssignmentView(staff.StaffMemberId, staff.UserId, staff.Practice, staff.Revision,
                staff.RecordedAt, true, partnerActor, Now, DateOnly.FromDateTime(Now.UtcDateTime))], "active",
            partnerActor, Now);

        var operationId = await ServiceEngagementFacts.AcceptAsync(provider, tenant, engagementId, acceptance,
            partnerActor, Now);
        await PracticeCommitmentFacts.CommitAsync(provider, tenant, engagementId, operationId, staff, partnerActor, Now);
        if (revoked)
            await ServiceEngagementFacts.CloseAcceptedAsync(provider, tenant, engagementId, acceptance, client,
                Now.AddMinutes(1));
        return (staff, engagementId);
    }
}
