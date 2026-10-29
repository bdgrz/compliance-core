using Cntryl.Portia;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
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
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class WorkforceRosterReconciliationTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 30);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Fact]
    public void ShouldReportNothingGivenCorrelatedActiveRoster()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var person = Person(tenantId, "Ada", "ada@example.com", userId);

        // Act
        var observations = WorkforceRosterReconciliation.Evaluate(tenantId, [person],
            [Relationship(tenantId, person.PersonId, "E-1", "active")],
            [Member(tenantId, userId)], Today);

        // Assert
        Assert.Empty(observations);
    }

    [Fact]
    public void ShouldReportNothingGivenDeprovisionedCorrelatedAndUnrosteredMembers()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var departedUser = Uuid.CreateVersion4();
        var unrosteredUser = Uuid.CreateVersion4();
        var departed = Person(tenantId, "Ada", null, departedUser);

        // Act
        var observations = WorkforceRosterReconciliation.Evaluate(tenantId, [departed],
            [Relationship(tenantId, departed.PersonId, "E-1", "ended")],
            [Member(tenantId, departedUser) with { IsDeprovisioned = true },
             Member(tenantId, unrosteredUser) with { IsDeprovisioned = true }], Today);

        // Assert
        Assert.Empty(observations);
    }

    [Fact]
    public void ShouldReportMissingDuplicateConflictingStaleAndAccessOnlyGivenManualRoster()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var sharedUser = Uuid.CreateVersion4();
        var leaverUser = Uuid.CreateVersion4();
        var unrosteredUser = Uuid.CreateVersion4();
        var departedUser = Uuid.CreateVersion4();
        var firmUser = Uuid.CreateVersion4();
        var missing = Person(tenantId, "Mia", "shared@example.com", null);
        var twinA = Person(tenantId, "Tom A", null, sharedUser);
        var twinB = Person(tenantId, "Tom B", "SHARED@example.com", sharedUser);
        var leaver = Person(tenantId, "Lee", null, leaverUser);
        var departed = Person(tenantId, "Dee", null, departedUser);
        WorkRelationshipView[] relationships =
        [
            Relationship(tenantId, twinA.PersonId, "E-1", "active"),
            Relationship(tenantId, twinB.PersonId, "E-2", "active"),
            Relationship(tenantId, leaver.PersonId, "E-3", "ended", endDate: Today.AddDays(-3)),
            Relationship(tenantId, departed.PersonId, "E-4", "active", endDate: Today.AddDays(-1)),
            Relationship(tenantId, departed.PersonId, "E-5", "pending",
                startDate: Today.AddDays(-2)),
        ];
        TenantMembershipView[] memberships =
        [
            Member(tenantId, sharedUser), Member(tenantId, leaverUser),
            Member(tenantId, unrosteredUser),
            Member(tenantId, Uuid.CreateVersion4()) with { IsSuspended = true },
            Member(tenantId, firmUser) with { Affiliation = "firm_staff" },
        ];

        // Act
        var observations = WorkforceRosterReconciliation.Evaluate(tenantId,
            [missing, twinA, twinB, leaver, departed], relationships, memberships, Today);
        var reasons = observations.Select(item => $"{item.Kind}:{item.Reason}").Order().ToList();
        var again = WorkforceRosterReconciliation.Evaluate(tenantId,
            [departed, leaver, twinB, twinA, missing], [.. relationships.Reverse()], memberships,
            Today);

        // Assert
        Assert.Equal(
        [
            "access_only:member_not_on_roster",
            "conflicting:ended_worker_retains_access",
            "duplicate:member_correlated_to_several_people",
            "duplicate:shared_work_email",
            "missing:no_work_relationship",
            "stale:correlated_member_missing",
            "stale:end_date_passed",
            "stale:start_date_passed",
        ], reasons);
        Assert.All(observations, item => Assert.Equal("open", item.Status));
        Assert.All(observations, item => Assert.Equal(tenantId, item.TenantId));
        Assert.Equal([unrosteredUser],
            observations.Single(item => item.Kind == "access_only").UserIds);
        Assert.Equal(new[] { missing.PersonId, twinB.PersonId }
                .OrderBy(id => id.ToString(), StringComparer.Ordinal),
            observations.Single(item => item.Reason == "shared_work_email").PersonIds);
        Assert.Equal([leaverUser],
            observations.Single(item => item.Kind == "conflicting").UserIds);
        Assert.Equal(observations.Select(item => item.ObservationId).Order(),
            again.Select(item => item.ObservationId).Order());
        Assert.Equal(observations.Count,
            observations.Select(item => item.ObservationId).Distinct().Count());
    }

    [Fact]
    public void ShouldIssueNewObservationGivenStaleRelationshipRevised()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var person = Person(tenantId, "Ada", null, userId);
        var stale = Relationship(tenantId, person.PersonId, "E-1", "active",
            endDate: Today.AddDays(-1));

        // Act
        var first = Assert.Single(WorkforceRosterReconciliation.Evaluate(tenantId, [person],
            [stale], [Member(tenantId, userId)], Today));
        var later = Assert.Single(WorkforceRosterReconciliation.Evaluate(tenantId, [person],
            [stale with { Revision = 2, EndDate = Today.AddDays(-2) }], [Member(tenantId, userId)],
            Today));

        // Assert
        Assert.NotEqual(first.ObservationId, later.ObservationId);
    }

    static PersonView Person(Uuid tenantId, string name, string? email, Uuid? userId) =>
        new(tenantId, Uuid.CreateVersion4(), 1, name, email, "manual", Author, Now, userId);

    static WorkRelationshipView Relationship(Uuid tenantId, Uuid personId, string workerId,
        string status, DateOnly? startDate = null, DateOnly? endDate = null) =>
        new(tenantId, WorkRelationship.IdFor(tenantId, workerId), 1, personId, workerId,
            "employee", status, startDate ?? new DateOnly(2025, 1, 6), endDate, null, null, null,
            false, "manual", Author, Now);

    static TenantMembershipView Member(Uuid tenantId, Uuid userId) => new(userId, tenantId);
}
