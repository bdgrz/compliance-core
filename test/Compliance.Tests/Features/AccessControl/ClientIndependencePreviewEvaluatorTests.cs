using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ClientIndependencePreviewEvaluatorTests
{
    static readonly Uuid Client = Uuid.CreateVersion4();
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Synthetic administrator");
    static readonly DateTimeOffset Now = IndependenceHost.At;

    [Fact]
    public void ShouldRetainFullHistoryGivenServiceOutsideTheLookBackWindow()
    {
        // Arrange
        var old = Service("design", new DateOnly(2023, 1, 1), new DateOnly(2023, 2, 1));

        // Act
        var result = Evaluate([old]);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("allowed", result.Value!.DecisionCode);
        Assert.Equal("compatible", result.Value.Outcome);
        Assert.Equal(old.ServiceRecordId, Assert.Single(result.Value.CompleteServiceHistory).ServiceRecordId);
        Assert.Empty(result.Value.ConsideredServiceRecordIds);
        Assert.True(result.Value.ProductionAcceptanceBlocked);
        Assert.False(result.Value.EvaluatedRules.IsRatified);
    }

    [Theory]
    [InlineData("design", "recent_impairing_service", "impaired")]
    [InlineData("readiness", "partner_evaluation_required", "conditionally_compatible")]
    [InlineData("unknown", "service_not_classified", null)]
    public void ShouldRetainDenialGivenRecentService(string kind, string code, string? outcome)
    {
        // Arrange
        var recent = Service(kind, new DateOnly(2026, 1, 1), null);

        // Act
        var result = Evaluate([recent]);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(code, result.Value!.DecisionCode);
        Assert.Equal(outcome, result.Value.Outcome);
        Assert.Equal(recent.ServiceRecordId, Assert.Single(result.Value.ConsideredServiceRecordIds));
        Assert.True(result.Value.ProductionAcceptanceBlocked);
    }

    [Fact]
    public void ShouldEvaluateEveryServiceGivenHistoryFarBeyondTheFormerEventBudget()
    {
        // Arrange
        var history = Enumerable.Range(0, 400).Select(index => Service("design", new DateOnly(2023, 1, 1),
            new DateOnly(2023, 2, 1)) with
        {
            Content = Service("design", new DateOnly(2023, 1, 1),
            new DateOnly(2023, 2, 1)).Content with
            { SourceReference = new string('s', 2000) + index }
        }).ToArray();

        // Act
        var result = Evaluate(history);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(400, result.Value!.CompleteServiceHistory.Count);
        Assert.Equal(history.Select(service => service.ServiceRecordId),
            result.Value.CompleteServiceHistory.Select(service => service.ServiceRecordId));
    }

    [Fact]
    public void ShouldProtectRetainedSnapshotsGivenMutableInputs()
    {
        // Arrange
        var entries = IndependenceHost.DefaultRules.ServiceRules.ToList();
        var staff = new List<Uuid> { Uuid.CreateVersion4() };
        var service = Service("design", new DateOnly(2026, 1, 1), null);
        service = service with { Content = service.Content with { FirmStaffMemberIds = staff } };
        var version = new IndependenceRuleVersionView(1, IndependenceHost.DefaultRules with { ServiceRules = entries },
            Actor, Now, false);
        var result = ClientIndependencePreviewEvaluator.Evaluate(Client, Uuid.CreateVersion4(), version,
            new DateOnly(2026, 10, 7), [service], Actor, Now);

        // Act
        entries.Clear();
        staff.Clear();

        // Assert
        Assert.Equal(2, result.Value!.EvaluatedRules.Content.ServiceRules.Count);
        Assert.Single(Assert.Single(result.Value.CompleteServiceHistory).Content.FirmStaffMemberIds);
    }

    [Fact]
    public void ShouldRejectGivenRatifiedRulesInvalidVersionDateOrAttribution()
    {
        // Arrange
        var rules = new IndependenceRuleVersionView(1, IndependenceHost.DefaultRules, Actor, Now, false);

        // Act
        var results = new[]
        {
            Evaluate([], rules with { IsRatified = true }),
            Evaluate([], rules with { Version = 0 }),
            Evaluate([], rules, DateOnly.MinValue),
            Evaluate([], rules with { Content = IndependenceHost.DefaultRules with { LookBackMonths = 11 } }),
            ClientIndependencePreviewEvaluator.Evaluate(Client, Uuid.CreateVersion4(), rules, new DateOnly(2026, 10, 7),
                [], ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), Now),
        };

        // Assert
        Assert.All(results, result => Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind));
    }

    static Result<IndependenceEvaluationView> Evaluate(IReadOnlyList<NonattestServiceView> history,
        IndependenceRuleVersionView? rules = null, DateOnly? period = null) =>
        ClientIndependencePreviewEvaluator.Evaluate(Client, Uuid.CreateVersion4(),
            rules ?? new IndependenceRuleVersionView(1, IndependenceHost.DefaultRules, Actor, Now, false),
            period ?? new DateOnly(2026, 10, 7), history, Actor, Now);

    static NonattestServiceView Service(string kind, DateOnly start, DateOnly? end) => new(Client,
        Uuid.CreateVersion4(), IndependenceHost.Service(kind, start, end), Actor, Now);
}
