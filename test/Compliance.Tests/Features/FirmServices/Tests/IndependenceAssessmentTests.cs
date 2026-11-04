using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
using Bdgrz.Compliance.Features.FirmServices.Services;
namespace Bdgrz.Compliance.Tests.Features.FirmServices.Tests;

public sealed class IndependenceAssessmentTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Start = new(2026, 1, 1);
    static readonly DateOnly End = new(2026, 12, 31);
    static IndependenceRuleSetView Rules => new(1, 2, IndependenceRuleSetTerms.Initial, true, UserId, Now);
    static NonattestServiceRecordView Service(DateOnly start, DateOnly? end, string kind = "control_design",
        bool voided = false) => new(TenantId, Uuid.CreateVersion4(), 1, new(kind, start, end), voided, null, UserId, Now);

    [Theory]
    [InlineData(2025, 1, 1)]
    [InlineData(2026, 1, 1)]
    [InlineData(2026, 8, 1)]
    [InlineData(2026, 12, 31)]
    public void ShouldReportImpairedGivenServiceOverlapsLookBackOrExamination(int year, int month, int day)
    {
        // Arrange
        var on = new DateOnly(year, month, day);
        var service = Service(on, on);

        // Act
        var result = IndependenceAssessment.Evaluate(TenantId, Rules, [service], Start, End);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("impaired", result.Value!.Result);
        Assert.Equal(new NonattestServiceVersion(service.RecordId, 1), Assert.Single(result.Value.ServicesConsidered));
    }

    [Theory]
    [InlineData(2024, 12, 31)]
    [InlineData(2027, 1, 1)]
    public void ShouldReportCompatibleGivenServiceOutsideWindow(int year, int month, int day)
    {
        // Arrange
        var on = new DateOnly(year, month, day);

        // Act
        var result = IndependenceAssessment.Evaluate(TenantId, Rules, [Service(on, on)], Start, End);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("compatible", result.Value!.Result);
        Assert.Single(result.Value.ServicesConsidered);
    }

    [Fact]
    public void ShouldClearImpairmentAndRetainSourceRevisionGivenServiceVoided()
    {
        // Arrange
        var service = Service(Start, null) with { Revision = 2, IsVoided = true };

        // Act
        var result = IndependenceAssessment.Evaluate(TenantId, Rules, [service], Start, End);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("compatible", result.Value!.Result);
        Assert.Equal(2, Assert.Single(result.Value.ServicesConsidered).Revision);
    }

    [Fact]
    public void ShouldRequirePartnerConclusionGivenConditionalServiceInHistory()
    {
        // Arrange
        var service = Service(new DateOnly(2020, 1, 1), new DateOnly(2020, 2, 1), "readiness_assessment");

        // Act
        var result = IndependenceAssessment.Evaluate(TenantId, Rules, [service], Start, End);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("conditionally_compatible", result.Value!.Result);
    }

    [Fact]
    public void ShouldInvalidateSnapshotGivenNewRevisionOrNewServiceOrNewRules()
    {
        // Arrange
        var service = Service(Start, null);
        var assessment = IndependenceAssessment.Evaluate(TenantId, Rules, [service], Start, End).Value!;

        // Act
        var same = IndependenceAssessment.MatchesCurrentSources(assessment, Rules, [service]);
        var corrected = IndependenceAssessment.MatchesCurrentSources(assessment, Rules, [service with { Revision = 2 }]);
        var added = IndependenceAssessment.MatchesCurrentSources(assessment, Rules, [service, Service(Start, null)]);
        var rules = IndependenceAssessment.MatchesCurrentSources(assessment, Rules with { Version = 2 }, [service]);

        // Assert
        Assert.True(same);
        Assert.False(corrected);
        Assert.False(added);
        Assert.False(rules);
    }

    [Fact]
    public void ShouldRefuseGivenForeignOrDuplicateSourceIdentity()
    {
        // Arrange
        var service = Service(Start, null);

        // Act
        var foreign = IndependenceAssessment.Evaluate(TenantId, Rules, [service with { TenantId = Uuid.CreateVersion4() }], Start, End);
        var duplicate = IndependenceAssessment.Evaluate(TenantId, Rules, [service, service], Start, End);

        // Assert
        Assert.False(foreign.IsSuccess);
        Assert.False(duplicate.IsSuccess);
    }
}
