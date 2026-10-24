using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderAssurancePipelineTests
{
    static readonly string[] Manager = [RbacPermissions.TenantAccess, RbacPermissions.ProviderInventoryManage];

    [Fact]
    public async Task ShouldRecordReportAndPersonalReviewAndEvaluateCoverageGivenProviderManager()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        host.Permissions.Granted = Manager;
        var reportId = Uuid.CreateVersion4();
        var request = new RecordAssuranceReport(reportId, host.Tenant, host.ProviderId, Report());

        // Act
        var report = (await host.SendAsync(request)).Value;
        var retry = (await host.SendAsync(request)).Value;
        var revised = (await host.SendAsync(new ReviseAssuranceReport(Uuid.CreateVersion4(), host.Tenant, host.ProviderId, reportId, 1,
            Report() with { BridgeLetter = new AssuranceBridgeLetter(new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 31)) }))).Value;
        var stale = await host.SendAsync(new ReviseAssuranceReport(Uuid.CreateVersion4(), host.Tenant, host.ProviderId, reportId, 1, Report()));
        var reviewId = Uuid.CreateVersion4();
        var admitted = await host.SendAsync(new RecordProviderReview(reviewId, host.Tenant, host.ProviderId, Review(reportId)));
        await host.DriveReviewAsync(reviewId);
        await host.CatchUpAsync();
        var coverage = (await host.SendAsync(new GetProviderAssuranceCoverage(host.Tenant, host.ProviderId, new DateOnly(2026, 10, 2)))).Value;
        var review = (await host.ReviewAsync(reviewId)).Recorded!;
        var owner = await host.ReportAsync(reportId);

        // Assert
        Assert.Equal(report, retry);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(2, revised.Revision);
        Assert.Contains("Current revision: 2", stale.Error!.Message, StringComparison.Ordinal);
        Assert.Equal("current", coverage.Status);
        Assert.False(coverage.Complete);
        Assert.Equal(["bridged_only"], coverage.Reasons);
        Assert.Equal(reviewId, coverage.LatestReviewId);
        Assert.Equal(RbacIds.Member(host.Tenant, host.User).ToString(), review.Actor.Id);
        Assert.Equal(1, review.ProviderRevision);
        Assert.Equal(2, review.AssuranceReportRevision);
        Assert.Equal(2, owner.Revision);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldAllowReadAndDenyWritesGivenAuthorityLevel(bool manager)
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        host.Permissions.Granted = manager ? Manager : [RbacPermissions.TenantAccess];

        // Act
        var coverage = await host.SendAsync(new GetProviderAssuranceCoverage(host.Tenant, host.ProviderId));
        var write = await host.SendAsync(new RecordAssuranceReport(Uuid.CreateVersion4(), host.Tenant, host.ProviderId, Report()));
        var review = await host.SendAsync(new RecordProviderReview(Uuid.CreateVersion4(), host.Tenant, host.ProviderId,
            Review() with { Evidence = Citation() }));

        // Assert
        Assert.Equal("no_review", coverage.Value.Status);
        Assert.Equal(manager, write.IsSuccess);
        Assert.Equal(manager, review.IsSuccess);
        if (!manager)
        {
            Assert.Equal(RequestErrorKind.Forbidden, write.Error?.Kind);
            Assert.Equal(RequestErrorKind.Forbidden, review.Error?.Kind);
        }
    }

    [Fact]
    public async Task ShouldHideForeignProviderGivenOtherTenantRequests()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        host.Permissions.Granted = Manager;
        var otherTenant = Uuid.CreateVersion4();

        // Act
        var record = await host.SendAsync(new RecordAssuranceReport(Uuid.CreateVersion4(), otherTenant, host.ProviderId, Report()));
        var review = await host.SendAsync(new RecordProviderReview(Uuid.CreateVersion4(), otherTenant, host.ProviderId,
            Review() with { Evidence = Citation() }));
        var coverage = await host.SendAsync(new GetProviderAssuranceCoverage(otherTenant, host.ProviderId));
        var unknown = await host.SendAsync(new GetProviderAssuranceCoverage(host.Tenant, Uuid.CreateVersion4()));
        var foreignReport = await host.RecordReportAsync();
        var foreignRevision = await host.SendAsync(new ReviseAssuranceReport(Uuid.CreateVersion4(), otherTenant, host.ProviderId,
            foreignReport.ReportId, 1, Report()));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, record.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, review.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, coverage.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error?.Kind);
        Assert.Equal(record.Error!.Message, coverage.Error!.Message);
        Assert.Equal(review.Error!.Message, unknown.Error!.Message);
        Assert.Equal(record.Error.Message, foreignRevision.Error!.Message);
    }

    [Fact]
    public async Task ShouldValidateGovernedArtifactReferenceGivenCitation()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        host.Permissions.Granted = Manager;
        var artifactId = Uuid.CreateVersion4();
        await host.SeedAsync(new EvidenceArtifact(host.Tenant, artifactId), artifact =>
            artifact.Register(new EvidenceArtifactContent("SOC 2 report", null, "report", "manual", Now,
                new DateOnly(2026, 6, 30), new DateOnly(2026, 6, 30), "restricted"), new string('a', 64), 1, Author, Now));

        // Act
        var found = await host.SendAsync(new RecordAssuranceReport(Uuid.CreateVersion4(), host.Tenant, host.ProviderId,
            Report("restricted") with { Citation = Citation("restricted", artifactId) }));
        var missing = await host.SendAsync(new RecordAssuranceReport(Uuid.CreateVersion4(), host.Tenant, host.ProviderId,
            Report() with { Citation = Citation("internal", Uuid.CreateVersion4()) }));
        var review = await host.SendAsync(new RecordProviderReview(Uuid.CreateVersion4(), host.Tenant, host.ProviderId,
            Review() with { Evidence = Citation("internal", Uuid.CreateVersion4()) }));

        // Assert
        Assert.Equal(1, found.Value.Revision);
        Assert.Equal(RequestErrorKind.NotFound, missing.Error?.Kind);
        Assert.Contains("artifact", missing.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RequestErrorKind.NotFound, review.Error?.Kind);
        Assert.Contains("artifact", review.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldLagListsUntilProjectionCatchesUpGivenRecordedReportAndReview()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        host.Permissions.Granted = Manager;
        var report = await host.RecordReportAsync();
        var reviewId = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new RecordProviderReview(reviewId, host.Tenant, host.ProviderId, Review(report.ReportId)))).IsSuccess);
        await host.DriveReviewAsync(reviewId);

        // Act
        var lagging = await host.SendAsync(new ListProviderAssuranceReports(host.Tenant, host.ProviderId));
        var laggingReviews = await host.SendAsync(new ListProviderReviews(host.Tenant, host.ProviderId));
        var laggingCoverage = await host.SendAsync(new GetProviderAssuranceCoverage(host.Tenant, host.ProviderId));
        await host.CatchUpAsync();
        var reports = (await host.SendAsync(new ListProviderAssuranceReports(host.Tenant, host.ProviderId))).Value;
        var reviews = (await host.SendAsync(new ListProviderReviews(host.Tenant, host.ProviderId))).Value;
        var invalidLimit = await host.SendAsync(new ListProviderReviews(host.Tenant, host.ProviderId, 0));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, lagging.Error?.Kind);
        Assert.True(lagging.Error!.IsTransient);
        Assert.True(laggingReviews.Error!.IsTransient);
        Assert.True(laggingCoverage.Error!.IsTransient);
        Assert.Equal(report.ReportId, Assert.Single(reports.Items).ReportId);
        Assert.False(reports.Items[0].Redacted);
        Assert.Equal(report.ReportId, Assert.Single(reviews.Items).Content.AssuranceReportId);
        Assert.Equal(RequestErrorKind.Validation, invalidLimit.Error?.Kind);
    }

    [Fact]
    public async Task ShouldNotLeakAcrossTenantsGivenTwoTenantsWithSameProviderIdentity()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        host.Permissions.Granted = Manager;
        var otherTenant = Uuid.CreateVersion4();
        await NamedRecordFacts.SeedProviderAsync(host.Services, otherTenant, host.ProviderId,
            MaterialProvider("Other tenant provider"), Author, Now);
        var report = await host.RecordReportAsync();
        var reviewId = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new RecordProviderReview(reviewId, host.Tenant, host.ProviderId, Review(report.ReportId)))).IsSuccess);
        await host.DriveReviewAsync(reviewId);
        await host.CatchUpAsync();
        await AssuranceProjectionCatchUp.RunAsync(host.Services, otherTenant);

        // Act
        var ownReports = (await host.SendAsync(new ListProviderAssuranceReports(host.Tenant, host.ProviderId))).Value;
        var otherReports = (await host.SendAsync(new ListProviderAssuranceReports(otherTenant, host.ProviderId))).Value;
        var otherReviews = (await host.SendAsync(new ListProviderReviews(otherTenant, host.ProviderId))).Value;
        var otherCoverage = (await host.SendAsync(new GetProviderAssuranceCoverage(otherTenant, host.ProviderId))).Value;
        var foreignProvider = await host.SendAsync(new ListProviderAssuranceReports(otherTenant, Uuid.CreateVersion4()));

        // Assert
        Assert.Single(ownReports.Items);
        Assert.Empty(otherReports.Items);
        Assert.Empty(otherReviews.Items);
        Assert.Equal("no_review", otherCoverage.Status);
        Assert.Empty(otherCoverage.Reports);
        Assert.Equal(RequestErrorKind.NotFound, foreignProvider.Error?.Kind);
    }
}
