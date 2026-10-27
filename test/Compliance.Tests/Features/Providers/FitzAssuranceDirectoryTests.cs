using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Providers;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class FitzAssuranceDirectoryTests
{
    [Fact]
    public async Task ShouldProjectProviderCoverageGapAdmissionLinkAndClosureGivenTenantScopedReads()
    {
        // Arrange
        var directory = new FitzAssuranceDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var gapId = Uuid.CreateVersion4();
        var content = new ProviderCoverageGapContent(Uuid.CreateVersion4(), "Payroll service",
            "Availability assertion", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30),
            "assurance_report", Uuid.CreateVersion4(), 2, "The period is only partly covered.");
        var recorded = new ProviderCoverageGapView(tenantId, gapId, providerId, 3, 1, content, "open", Author, Now,
            null, []);
        var link = new ProviderCoverageGapRiskAcceptanceView(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Now.AddMonths(2), Author, Now.AddHours(1))
        {
            Revision = 2,
        };
        var closure = new ProviderCoverageGapClosureView(
            new ProviderCoverageGapClosureContent("coverage_restored", "assurance_report", Uuid.CreateVersion4(), 3,
                "A newer report covers the period."), Author, Now.AddDays(1))
        {
            Revision = 3,
        };

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await Apply(directory, tenantId, providerId, gapId, recorded, link, closure);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        await using (var replay = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await Apply(directory, tenantId, providerId, gapId, recorded, link, closure);
            await replay.CommitAsync(ProjectionCheckpoint.Start);
        }
        var gap = await directory.GetCoverageGapAsync(tenantId, providerId, gapId);
        var listed = await directory.ListCoverageGapsAsync(tenantId, providerId, 10, null);
        var foreign = await directory.GetCoverageGapAsync(otherTenantId, providerId, gapId);
        var otherProvider = await directory.GetCoverageGapAsync(tenantId, Uuid.CreateVersion4(), gapId);
        var foreignList = await directory.ListCoverageGapsAsync(otherTenantId, providerId, 10, null);

        // Assert
        Assert.Equal(content, gap!.Content);
        Assert.Equal(3, gap.ProviderRevision);
        Assert.Equal(3, gap.Revision);
        Assert.Equal("closed", gap.Status);
        Assert.Equal(link, Assert.Single(gap.RiskAcceptances!));
        Assert.Equal("coverage_restored", gap.Closure!.Content.Resolution);
        Assert.Single(listed.Items);
        Assert.Null(foreign);
        Assert.Null(otherProvider);
        Assert.Empty(foreignList.Items);
        await using var changed = await directory.BeginAsync(new ProjectionBatchContext(
            Identity(tenantId), ProjectionCheckpoint.Start));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyGapLinkAsync(tenantId, providerId, gapId, link with { ExpiresAt = link.ExpiresAt.AddDays(1) }));
    }

    [Fact]
    public async Task ShouldKeepGapStatusAndRevisionGivenLinkMaterializedAfterClosure()
    {
        // Arrange
        var directory = new FitzAssuranceDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var gapId = Uuid.CreateVersion4();
        var recorded = new ProviderCoverageGapView(tenantId, gapId, providerId, 1, 1, Gap(), "open", Author, Now, null, []);
        var link = new ProviderCoverageGapRiskAcceptanceView(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Now.AddMonths(2), Author, Now)
        {
            Revision = 2,
        };
        var closure = new ProviderCoverageGapClosureView(new ProviderCoverageGapClosureContent("coverage_restored",
            "assurance_report", Uuid.CreateVersion4(), 3, "Covered."), Author, Now.AddDays(1))
        {
            Revision = 3,
        };

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyGapRecordedAsync(recorded);
            await directory.ApplyGapAdmissionAsync(tenantId, providerId, gapId, 2);
            await directory.ApplyGapClosedAsync(tenantId, providerId, gapId, 3, closure);
            await directory.ApplyGapLinkAsync(tenantId, providerId, gapId, link);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var gap = await directory.GetCoverageGapAsync(tenantId, providerId, gapId);

        // Assert
        Assert.Equal("closed", gap!.Status);
        Assert.Equal(3, gap.Revision);
        Assert.Equal(2, gap.RiskAcceptances![0].Revision);
    }

    [Fact]
    public async Task ShouldExhaustScopedPagesGivenTwoTenantsAndRetainedReportsAndReviews()
    {
        // Arrange
        var directory = new FitzAssuranceDirectory(new InMemoryKvClient());
        Uuid[] tenants = [Uuid.CreateVersion4(), Uuid.CreateVersion4()];
        Uuid[] providers = [Uuid.CreateVersion4(), Uuid.CreateVersion4()];
        var reports = new Uuid[2][];
        foreach (var index in Enumerable.Range(0, 2))
        {
            reports[index] = [Uuid.CreateVersion4(), Uuid.CreateVersion4()];
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenants[index]), ProjectionCheckpoint.Start));
            foreach (var reportId in reports[index])
            {
                await directory.ApplyReportAsync(ReportView(tenants[index], providers[index], reportId, 1, Report()));
                await directory.ApplyReportAsync(ReportView(tenants[index], providers[index], reportId, 2,
                    Report() with { Issuer = "Revised issuer" }));
                await directory.ApplyReviewAsync(ReviewView(tenants[index], providers[index], Uuid.CreateVersion4(),
                    Review(reportId), 1, 2));
            }
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var seen = new List<AssuranceReportView>();
        var reviews = new List<ProviderReviewView>();
        for (var index = 0; index < 2; index++)
        {
            string? cursor = null;
            do
            {
                var page = await directory.ListReportsAsync(tenants[index], providers[index], 1, cursor);
                seen.AddRange(page.Items);
                cursor = page.NextCursor;
            } while (cursor is not null);
            do
            {
                var page = await directory.ListReviewsAsync(tenants[index], providers[index], 1, cursor);
                reviews.AddRange(page.Items);
                cursor = page.NextCursor;
            } while (cursor is not null);
        }
        var firstReports = await directory.ListReportsAsync(tenants[0], providers[0], 1, null);
        var firstReviews = await directory.ListReviewsAsync(tenants[0], providers[0], 1, null);
        var foreignTenant = await directory.ListReportsAsync(tenants[1], providers[0], 10, null);
        var foreignProvider = await directory.ListReviewsAsync(tenants[0], providers[1], 10, null);
        var retained = await directory.GetReportRevisionAsync(tenants[0], providers[0], reports[0][0], 1);
        var wrongProvider = await directory.GetReportRevisionAsync(tenants[0], providers[1], reports[0][0], 1);

        // Assert
        Assert.Equal(reports.SelectMany(static ids => ids).Order(), seen.Select(static view => view.ReportId).Order());
        Assert.All(seen, view => { Assert.Equal(2, view.Revision); Assert.Equal("Revised issuer", view.Content.Issuer); });
        Assert.Equal(4, reviews.Count);
        Assert.Empty(foreignTenant.Items);
        Assert.Empty(foreignProvider.Items);
        Assert.Equal("Example Assurance LLP", retained!.Content.Issuer);
        Assert.Null(wrongProvider);
        await Assert.ThrowsAsync<KvDirectoryQueryException>(async () => await directory.ListReportsAsync(tenants[1], providers[1], 1, firstReports.NextCursor));
        await Assert.ThrowsAsync<KvDirectoryQueryException>(async () => await directory.ListReviewsAsync(tenants[1], providers[1], 1, firstReviews.NextCursor));
        await Assert.ThrowsAsync<KvDirectoryQueryException>(async () => await directory.ListReportsAsync(tenants[0], providers[1], 1, firstReports.NextCursor));
    }

    [Fact]
    public async Task ShouldKeepTwoProvidersRecordsApartGivenTheSameCallerChosenReportId()
    {
        // Arrange
        var directory = new FitzAssuranceDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var reportId = Uuid.CreateVersion4();
        Uuid[] providers = [Uuid.CreateVersion4(), Uuid.CreateVersion4()];

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyReportAsync(ReportView(tenantId, providers[0], reportId, 1, Report()));
            await directory.ApplyReportAsync(ReportView(tenantId, providers[1], reportId, 1, Report() with { Issuer = "Other" }));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Equal("Example Assurance LLP", Assert.Single((await directory.ListReportsAsync(tenantId, providers[0], 10, null)).Items).Content.Issuer);
        Assert.Equal("Other", Assert.Single((await directory.ListReportsAsync(tenantId, providers[1], 10, null)).Items).Content.Issuer);
    }

    [Fact]
    public async Task ShouldRetainStateAndRejectChangedReplayGivenCommittedFacts()
    {
        // Arrange
        var directory = new FitzAssuranceDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var reportId = Uuid.CreateVersion4();
        var first = ReportView(tenantId, providerId, reportId, 1, Report());
        var second = ReportView(tenantId, providerId, reportId, 2, Report() with { Issuer = "Revised" });
        var review = ReviewView(tenantId, providerId, Uuid.CreateVersion4(), Review(reportId), 1, 2);
        await using (var initial = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyReportAsync(first);
            await directory.ApplyReportAsync(second);
            await directory.ApplyReviewAsync(review);
            await initial.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        await using (var replay = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyReportAsync(first);
            await directory.ApplyReportAsync(second);
            await directory.ApplyReviewAsync(review);
            await replay.CommitAsync(ProjectionCheckpoint.Start);
        }
        await using (var changed = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyReportAsync(
                second with { Content = second.Content with { Issuer = "Replacement" } }));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyReviewAsync(
                review with { Content = review.Content with { Rationale = "Replacement" } }));
        }

        // Assert
        var reports = await directory.ListReportsAsync(tenantId, providerId, 10, null);
        var current = Assert.Single(reports.Items);
        Assert.Equal(2, current.Revision);
        Assert.Equal("Revised", current.Content.Issuer);
        Assert.Single((await directory.ListReviewsAsync(tenantId, providerId, 10, null)).Items);
    }

    [Theory]
    [InlineData("foreign_tenant")]
    [InlineData("empty_tenant")]
    [InlineData("empty_provider")]
    [InlineData("revision_gap")]
    [InlineData("revision_without_predecessor")]
    public async Task ShouldRejectBeforeCommitGivenFactOutsideItsBatchOrOrder(string invalid)
    {
        // Arrange
        var directory = new FitzAssuranceDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var view = invalid switch
        {
            "revision_gap" => ReportView(tenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 3, Report()),
            "revision_without_predecessor" => ReportView(tenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2, Report()),
            "foreign_tenant" => ReportView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, Report()),
            "empty_tenant" => ReportView(Uuid.Empty, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, Report()),
            _ => ReportView(tenantId, Uuid.Empty, Uuid.CreateVersion4(), 1, Report()),
        };

        // Act
        await using var batch = await ((IAssuranceProjection)directory).BeginAsync(
            new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start));

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyReportAsync(view));
    }

    [Fact]
    public async Task ShouldRecoverWithoutPartialRowsGivenAbortedProjectionBatch()
    {
        // Arrange
        var directory = new FitzAssuranceDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var reportId = Uuid.CreateVersion4();
        var first = ReportView(tenantId, providerId, reportId, 1, Report());

        // Act
        await using (var interrupted = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyReportAsync(first);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyReportAsync(
                ReportView(tenantId, providerId, reportId, 3, Report())));
        }
        var before = await directory.ListReportsAsync(tenantId, providerId, 10, null);
        await using (var retry = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyReportAsync(first);
            await retry.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Empty(before.Items);
        Assert.Single((await directory.ListReportsAsync(tenantId, providerId, 10, null)).Items);
    }

    static async Task Apply(FitzAssuranceDirectory directory, Uuid tenantId, Uuid providerId, Uuid gapId,
        ProviderCoverageGapView recorded, ProviderCoverageGapRiskAcceptanceView link, ProviderCoverageGapClosureView closure)
    {
        await directory.ApplyGapRecordedAsync(recorded);
        await directory.ApplyGapAdmissionAsync(tenantId, providerId, gapId, 2);
        await directory.ApplyGapLinkAsync(tenantId, providerId, gapId, link);
        await directory.ApplyGapClosedAsync(tenantId, providerId, gapId, closure.Revision, closure);
    }

    static ProviderCoverageGapContent Gap() => new(Uuid.CreateVersion4(), "Payroll service", "Availability assertion",
        new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), "assurance_report", Uuid.CreateVersion4(), 2,
        "The period is only partly covered.");

    static CheckpointIdentity Identity(Uuid tenantId) => new(FitzAssuranceDirectory.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), ProviderAssuranceStreams.Area));
}
