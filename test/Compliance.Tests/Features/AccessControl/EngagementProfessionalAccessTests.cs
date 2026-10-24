using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class EngagementProfessionalAccessTests
{
    static readonly DateTimeOffset At = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    static readonly Uuid Tenant = Uuid.Parse("00000000-0000-4000-8000-0000000000a1", System.Globalization.CultureInfo.InvariantCulture);
    static readonly Uuid Engagement = Uuid.Parse("00000000-0000-4000-8000-0000000000a2", System.Globalization.CultureInfo.InvariantCulture);
    static readonly Uuid Service = Uuid.Parse("00000000-0000-4000-8000-0000000000a3", System.Globalization.CultureInfo.InvariantCulture);
    static readonly Uuid Staff = Uuid.Parse("00000000-0000-4000-8000-0000000000a4", System.Globalization.CultureInfo.InvariantCulture);
    static readonly Uuid User = Uuid.Parse("00000000-0000-4000-8000-0000000000a5", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void ShouldGrantAccessGivenActiveAcceptanceInsidePeriodAtCurrentRevisionAndCompleteServiceHistory()
    {
        // Arrange
        var accepted = Accepted();

        // Act
        var eligible = EngagementProfessionalAccess.IsEligible(accepted, [Service], Staff, User, 1, 1, At.AddDays(1));

        // Assert
        Assert.True(eligible);
    }

    [Theory]
    [InlineData(2025, 12, 31)]
    [InlineData(2027, 1, 1)]
    public void ShouldDenyAccessGivenCurrentTimeOutsideTheEngagementPeriod(int year, int month, int day)
    {
        // Arrange
        var accepted = Accepted();
        var outside = new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero);

        // Act
        var eligible = EngagementProfessionalAccess.IsEligible(accepted, [Service], Staff, User, 1, 1, outside);

        // Assert
        Assert.False(eligible);
    }

    [Fact]
    public void ShouldDenyAccessGivenAcceptanceInstantNotYetReached()
    {
        // Arrange
        var accepted = Accepted();

        // Act
        var eligible = EngagementProfessionalAccess.IsEligible(accepted, [Service], Staff, User, 1, 1, At.AddMinutes(-1));

        // Assert
        Assert.False(eligible);
    }

    [Theory]
    [InlineData("new_service")]
    [InlineData("missing_service")]
    [InlineData("other_rules")]
    [InlineData("other_directory_revision")]
    [InlineData("other_staff")]
    [InlineData("other_user")]
    [InlineData("receipt")]
    [InlineData("closed_engagement")]
    [InlineData("closed_acceptance")]
    public void ShouldLoseAccessWithoutErasingHistoryGivenNewServiceFactsOrChangedExternalVersionsOrReceipt(string change)
    {
        // Arrange
        var accepted = change switch
        {
            "receipt" => Accepted() with { DeniedByReceipt = [(Staff, User)] },
            "closed_engagement" => Accepted() with { Engagement = Accepted().Engagement with { Status = "closed" } },
            "closed_acceptance" => Accepted() with { Acceptance = Accepted().Acceptance with { Status = "closed" } },
            _ => Accepted(),
        };
        var services = change switch
        {
            "new_service" => new[] { Service, Uuid.CreateVersion4() },
            "missing_service" => [],
            _ => new[] { Service },
        };

        // Act
        var eligible = EngagementProfessionalAccess.IsEligible(accepted, services,
            change == "other_staff" ? Uuid.CreateVersion4() : Staff, change == "other_user" ? Uuid.CreateVersion4() : User,
            change == "other_rules" ? 2 : 1, change == "other_directory_revision" ? 2 : 1, At.AddDays(1));

        // Assert
        Assert.False(eligible);
        Assert.Equal(Service, Assert.Single(accepted.Acceptance.CompleteServiceHistory).ServiceRecordId);
        Assert.True(Assert.Single(accepted.Acceptance.Assignments).StaffMemberId == Staff);
    }

    [Fact]
    public void ShouldDenyAccessOnlyForTheReceiptStaffGivenAnotherAssigneeStillCurrent()
    {
        // Arrange
        var other = (StaffMemberId: Uuid.CreateVersion4(), UserId: Uuid.CreateVersion4());
        var baseline = Accepted();
        var accepted = baseline with
        {
            Acceptance = baseline.Acceptance with
            {
                Assignments = [..baseline.Acceptance.Assignments, baseline.Acceptance.Assignments[0] with
                {
                    StaffMemberId = other.StaffMemberId,
                    UserId = other.UserId,
                }],
            },
            DeniedByReceipt = [(Staff, User)],
        };

        // Act
        var denied = EngagementProfessionalAccess.IsEligible(accepted, [Service], Staff, User, 1, 1, At.AddDays(1));
        var allowed = EngagementProfessionalAccess.IsEligible(accepted, [Service], other.StaffMemberId, other.UserId, 1, 1,
            At.AddDays(1));

        // Assert
        Assert.False(denied);
        Assert.True(allowed);
    }

    static AcceptedEngagement Accepted()
    {
        var actor = ActorReference.ForFirmStaff(Uuid.CreateVersion4(), "Synthetic partner");
        var rules = new IndependenceRuleVersionView(1, new IndependenceRuleContent(12, [], "Synthetic rules"), actor, At, true);
        var service = new NonattestServiceView(Tenant, Service, new NonattestServiceContent(Uuid.CreateVersion4(),
            "readiness", new DateOnly(2025, 12, 1), null, [Staff], false, "Synthetic source"), actor, At);
        var acceptance = new ServiceEngagementAcceptanceView(Tenant, Engagement, 1, 1, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, 1, "Synthetic authority", null, Uuid.CreateVersion4(), null,
            null, null, null, new EngagementAcceptanceSourceTimes(At, At, null, null), rules, [service], "allowed",
            "not_applicable", [],
            [new EngagementActualAssignmentView(Staff, User, "attest", 1, At, true, actor, At, new DateOnly(2026, 10, 8))],
            "active", actor, At);
        var engagement = new ServiceEngagementView(Tenant, Engagement, 2, new ServiceEngagementDraftContent("attest",
            "Synthetic scope", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Staff), "accepted", [], actor, At,
            false);
        return new AcceptedEngagement(engagement, acceptance, []);
    }
}
