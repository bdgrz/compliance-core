using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class EngagementAcceptancePolicyTests
{
    static readonly Uuid Tenant = Uuid.Parse("00000000-0000-4000-8000-0000000000b1", System.Globalization.CultureInfo.InvariantCulture);
    static readonly ActorReference Actor = ActorReference.ForFirmStaff(
        Uuid.Parse("00000000-0000-4000-8000-0000000000b2", System.Globalization.CultureInfo.InvariantCulture), "Synthetic");
    static readonly DateTimeOffset At = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldAllowAdvisoryEngagementGivenAnyServiceHistory()
    {
        // Arrange
        var history = new[] { Service("design", new DateOnly(2026, 1, 1)) };

        // Act
        var (decision, considered) = EngagementAcceptancePolicy.Evaluate(Tenant, "advisory", Rules(), history,
            new DateOnly(2026, 2, 1), false);

        // Assert
        Assert.Equal(IndependenceDecisionCode.Allowed, decision.Code);
        Assert.Empty(considered);
        Assert.Equal("not_applicable", EngagementAcceptanceViews.OutcomeOf(decision));
    }

    [Theory]
    [InlineData("compatible", false, IndependenceDecisionCode.Allowed, "compatible")]
    [InlineData("conditionally_compatible", false, IndependenceDecisionCode.PartnerEvaluationRequired, "conditionally_compatible")]
    [InlineData("conditionally_compatible", true, IndependenceDecisionCode.Allowed, "conditionally_compatible")]
    [InlineData("impairing", true, IndependenceDecisionCode.RecentImpairingService, "not_applicable")]
    public void ShouldComputePolicyFromCompleteRetainedHistoryGivenAttestEngagement(string classification,
        bool evaluationRecorded, IndependenceDecisionCode code, string outcome)
    {
        // Arrange
        var service = Service("readiness", new DateOnly(2025, 12, 1));

        // Act
        var (decision, considered) = EngagementAcceptancePolicy.Evaluate(Tenant, "attest", Rules(classification),
            [service], new DateOnly(2026, 1, 1), evaluationRecorded);

        // Assert
        Assert.Equal(code, decision.Code);
        Assert.Equal(outcome, EngagementAcceptanceViews.OutcomeOf(decision));
        Assert.Equal(service.ServiceRecordId, Assert.Single(considered));
    }

    [Fact]
    public void ShouldRefuseAttestEngagementGivenServiceTypeTheRulesDoNotClassify()
    {
        // Arrange
        var service = Service("unknown_kind", new DateOnly(2025, 12, 1));

        // Act
        var (decision, _) = EngagementAcceptancePolicy.Evaluate(Tenant, "attest", Rules("compatible"), [service],
            new DateOnly(2026, 1, 1), true);

        // Assert
        Assert.Equal(IndependenceDecisionCode.ServiceNotClassified, decision.Code);
    }

    [Fact]
    public void ShouldIgnoreServiceOutsideTheLookBackGivenAttestEngagement()
    {
        // Arrange
        var service = Service("readiness", new DateOnly(2020, 1, 1), new DateOnly(2020, 6, 1));

        // Act
        var (decision, considered) = EngagementAcceptancePolicy.Evaluate(Tenant, "attest", Rules("impairing"), [service],
            new DateOnly(2026, 1, 1), false);

        // Assert
        Assert.Equal(IndependenceDecisionCode.Allowed, decision.Code);
        Assert.Empty(considered);
    }

    static NonattestServiceView Service(string kind, DateOnly start, DateOnly? end = null) => new(Tenant,
        Uuid.CreateVersion4(), new NonattestServiceContent(Uuid.CreateVersion4(), kind, start, end,
            [Uuid.CreateVersion4()], false, "Synthetic source"), Actor, At);

    static IndependenceRuleVersionView Rules(string classification = "compatible") => new(1,
        new IndependenceRuleContent(12, [new IndependenceServiceRuleContent("readiness", classification, "impairing")],
            "Synthetic rules"), Actor, At, true);
}
