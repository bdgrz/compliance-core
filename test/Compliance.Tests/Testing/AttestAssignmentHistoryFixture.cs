using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
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
