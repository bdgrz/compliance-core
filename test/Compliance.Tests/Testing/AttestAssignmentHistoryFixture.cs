using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Synthetic internal authority fixtures only; never evidence of real professional designation or ratification.
///     The permanent actual-assignment wall is still read from the retained client ledger, so the fixture appends one
///     well-formed acceptance fact straight onto that ledger, exactly as an accepted engagement would leave it, next to
///     the engagement draft on its own owner. The assignments stay in the wall whether or not the engagement is later
///     closed; closure only closes the draft.
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

        await using var scope = provider.CreateAsyncScope();
        var ledger = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new IndependenceLedger(tenant));
        var accepted = DomainEventSeed.Attach(new ServiceEngagementAcceptanceRecorded(tenant, Uuid.CreateVersion4(),
            ledger.Sequence, "Synthetic accepted engagement", acceptance), ledger.Id, (ulong)ledger.Sequence + 1);
        await scope.ServiceProvider.GetRequiredService<IEventStore>()
            .AppendAsync(ledger.Stream, ledger.CommittedStreamPosition, [accepted]);
        if (revoked)
            await ServiceEngagementFacts.CloseAsync(provider, tenant, engagementId, draft, client, Now.AddMinutes(1));
        return (staff, engagementId);
    }
}
