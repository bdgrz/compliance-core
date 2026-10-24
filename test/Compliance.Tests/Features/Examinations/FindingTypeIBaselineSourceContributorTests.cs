using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Remediation;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class FindingTypeIBaselineSourceContributorTests
{
    [Fact]
    public async Task ShouldCaptureFindingWithItsActionHistoryGivenFactsInsideCutoff()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await FindingTests.RaiseWithCompletedActionAsync(fixture);
        var contributor = Contributor(fixture);

        // Act
        var result = await contributor.ReadAsync(Engagement(fixture, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)),
            new TypeIBaselineSourceReadContext(fixture.LeadMemberId, []), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("findings", result.Value.Family);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("finding", source.Kind);
        Assert.Equal(finding.FindingId, source.RecordId);
        Assert.Equal(2, source.Revision);
        Assert.Equal(Uuid.CreateVersion5(finding.FindingId, "finding_v1:2"), source.VersionId);
        Assert.Matches("^[0-9a-f]{64}$", source.ContentSha256);
    }

    [Fact]
    public async Task ShouldChangeDigestGivenLaterFactInsideCutoff()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var contributor = Contributor(fixture);
        var engagement = Engagement(fixture, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1));
        var before = await contributor.ReadAsync(engagement, new TypeIBaselineSourceReadContext(fixture.LeadMemberId, []),
            CancellationToken.None);

        // Act
        await fixture.AsAsync(fixture.LeadUserId, new ReviseFinding(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, finding.FindingId, 1, "critical", fixture.OwnerMemberId, fixture.Today.AddDays(3), "All",
            null, "Escalated."));
        var after = await contributor.ReadAsync(engagement, new TypeIBaselineSourceReadContext(fixture.LeadMemberId, []),
            CancellationToken.None);
        var again = await contributor.ReadAsync(engagement, new TypeIBaselineSourceReadContext(fixture.LeadMemberId, []),
            CancellationToken.None);

        // Assert
        Assert.Equal(1, Assert.Single(before.Value.Sources).Revision);
        var revised = Assert.Single(after.Value.Sources);
        Assert.Equal(2, revised.Revision);
        Assert.NotEqual(Assert.Single(before.Value.Sources).ContentSha256, revised.ContentSha256);
        Assert.Equal(revised.ContentSha256, Assert.Single(again.Value.Sources).ContentSha256);
    }

    [Fact]
    public async Task ShouldExcludeFindingGivenEnvelopeRecordedAfterCutoff()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var contributor = Contributor(fixture);

        // Act
        var result = await contributor.ReadAsync(Engagement(fixture, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2)),
            new TypeIBaselineSourceReadContext(fixture.LeadMemberId, []), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
    }

    static FindingTypeIBaselineSourceContributor Contributor(OperationsFixture fixture)
    {
        var scope = fixture.Provider.CreateScope();
        return new FindingTypeIBaselineSourceContributor(scope.ServiceProvider.GetRequiredService<IDomainEventReader>(),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>());
    }

    static ExaminationEngagementView Engagement(OperationsFixture fixture, DateOnly typeIDate) => new(fixture.TenantId,
        fixture.ProgramId, Uuid.CreateVersion4(), "type_i", "Audit Firm", typeIDate, null, ["security"],
        Uuid.CreateVersion4(), [], "draft", 1,
        new DateTimeOffset(typeIDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)), fixture.LeadMemberId, "Examiner");
}
