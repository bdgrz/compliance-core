using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderAssuranceReportTests
{
    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var reportId = Uuid.CreateVersion4();
        var commandId = Uuid.CreateVersion4();
        var revised = Report() with { Issuer = "Renamed Assurance LLP" };
        var first = await host.RecordReportAsync(reportId);
        var revision = await host.SendAsync(new ReviseAssuranceReport(commandId, host.Tenant, host.ProviderId, reportId, 1, revised));
        Assert.True(revision.IsSuccess, revision.Error?.Message);
        var facts = await host.FactsAsync(ProviderAssuranceStreams.Report(host.Tenant, host.ProviderId, reportId));

        // Act
        var retry = await host.SendAsync(new RecordAssuranceReport(reportId, host.Tenant, host.ProviderId, Report()));
        var revisionRetry = await host.SendAsync(new ReviseAssuranceReport(commandId, host.Tenant, host.ProviderId, reportId, 1, revised));

        // Assert
        Assert.Equal(first, retry.Value);
        Assert.Equal(1, retry.Value.Revision);
        Assert.Equal(2, revisionRetry.Value.Revision);
        Assert.Equal(2, (await host.FactsAsync(ProviderAssuranceStreams.Report(host.Tenant, host.ProviderId, reportId))).Count);
        Assert.Equal(2, facts.Count);
        var owner = await host.ReportAsync(reportId);
        Assert.Equal(2, owner.Revision);
        Assert.Equal("Renamed Assurance LLP", (await host.Content.ReadJsonAsync<AssuranceReportContent>(
            host.Tenant.ToString(), owner.LatestContent!)).Issuer);
    }

    [Fact]
    public async Task ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var reportId = Uuid.CreateVersion4();
        var commandId = Uuid.CreateVersion4();
        await host.RecordReportAsync(reportId);
        Assert.True((await host.SendAsync(new ReviseAssuranceReport(commandId, host.Tenant, host.ProviderId, reportId, 1,
            Report() with { Opinion = "qualified" }))).IsSuccess);

        // Act
        var changedIntent = await host.SendAsync(new RecordAssuranceReport(reportId, host.Tenant, host.ProviderId,
            Report() with { Issuer = "Other" }));
        var changedActor = await host.SendAsync(new RecordAssuranceReport(reportId, host.Tenant, host.ProviderId, Report()),
            user: Uuid.CreateVersion4());
        var changedRevisionIntent = await host.SendAsync(new ReviseAssuranceReport(commandId, host.Tenant, host.ProviderId,
            reportId, 1, Report() with { Opinion = "adverse" }));
        var changedExpected = await host.SendAsync(new ReviseAssuranceReport(commandId, host.Tenant, host.ProviderId,
            reportId, 2, Report() with { Opinion = "qualified" }));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changedIntent.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedActor.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedRevisionIntent.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedExpected.Error?.Kind);
        Assert.Equal(2, (await host.FactsAsync(ProviderAssuranceStreams.Report(host.Tenant, host.ProviderId, reportId))).Count);
    }

    [Fact]
    public async Task ShouldRejectGivenMissingMalformedOrConflictingRouteIdentity()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();

        // Act
        var noReport = await host.SendAsync(new RecordAssuranceReport(Uuid.Empty, host.Tenant, host.ProviderId, Report()));
        var noProvider = await host.SendAsync(new RecordAssuranceReport(Uuid.CreateVersion4(), host.Tenant, Uuid.Empty, Report()));
        var noCommand = await host.SendAsync(new ReviseAssuranceReport(Uuid.Empty, host.Tenant, host.ProviderId,
            Uuid.CreateVersion4(), 1, Report()));
        var noReportRevise = await host.SendAsync(new ReviseAssuranceReport(Uuid.CreateVersion4(), host.Tenant,
            host.ProviderId, Uuid.Empty, 1, Report()));
        var noRevision = await host.SendAsync(new ReviseAssuranceReport(Uuid.CreateVersion4(), host.Tenant, host.ProviderId,
            Uuid.CreateVersion4(), 0, Report()));

        // Assert
        Assert.All([noReport.Error, noProvider.Error, noCommand.Error, noReportRevise.Error, noRevision.Error],
            error => Assert.True(error?.Kind is RequestErrorKind.Validation or RequestErrorKind.NotFound));
        Assert.Equal(RequestErrorKind.Validation, noReport.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, noCommand.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectStaleRevisionAndForeignProviderGivenExistingReport()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var report = await host.RecordReportAsync();

        // Act
        var stale = await host.SendAsync(new ReviseAssuranceReport(Uuid.CreateVersion4(), host.Tenant, host.ProviderId,
            report.ReportId, 2, Report()));
        var otherProvider = await host.SendAsync(new ReviseAssuranceReport(Uuid.CreateVersion4(), host.Tenant,
            Uuid.CreateVersion4(), report.ReportId, 1, Report()));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Contains("Current revision: 1", stale.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.NotFound, otherProvider.Error?.Kind);
        Assert.Single(await host.FactsAsync(ProviderAssuranceStreams.Report(host.Tenant, host.ProviderId, report.ReportId)));
    }

    [Fact]
    public async Task ShouldRecordManyIndependentReportsGivenNoRetainedLimit()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var ids = new List<Uuid>();

        // Act
        for (var index = 0; index < 205; index++)
            ids.Add((await host.RecordReportAsync()).ReportId);
        await host.CatchUpAsync();
        var listed = new List<AssuranceReportView>();
        await using var scope = host.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IAssuranceReader>();
        string? cursor = null;
        do
        {
            var page = await directory.ListReportsAsync(host.Tenant, host.ProviderId, 50, cursor);
            listed.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);

        // Assert
        Assert.Equal(205, ids.Distinct().Count());
        Assert.Equal(205, listed.Count);
    }

    [Fact]
    public async Task ShouldKeepProvidersApartGivenTheSameReportIdentityOnTwoProviders()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var otherProvider = Uuid.CreateVersion4();
        await NamedRecordFacts.SeedProviderAsync(host.Services, host.Tenant, otherProvider,
            MaterialProvider("Other provider"), Author, Now);
        var reportId = Uuid.CreateVersion4();

        // Act
        var first = await host.SendAsync(new RecordAssuranceReport(reportId, host.Tenant, host.ProviderId, Report()));
        var second = await host.SendAsync(new RecordAssuranceReport(reportId, host.Tenant, otherProvider,
            Report() with { Issuer = "Other LLP" }));
        await host.CatchUpAsync();

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.NotEqual(ProviderAssuranceStreams.Report(host.Tenant, host.ProviderId, reportId),
            ProviderAssuranceStreams.Report(host.Tenant, otherProvider, reportId));
    }

    [Fact]
    public async Task ShouldRefuseRevisionGivenReportHeldByAdmittedReview()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var report = await host.RecordReportAsync();
        var reviewId = Uuid.CreateVersion4();
        var admitted = await host.SendAsync(new RecordProviderReview(reviewId, host.Tenant, host.ProviderId, Review(report.ReportId)));
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        var root = await host.RootAsync(ProviderAssuranceStreams.Review(host.Tenant, reviewId), reviewId);
        var requested = (ProviderReviewRequested)root.Event;
        var started = await host.PrepareProcessAsync(await host.ReviewKey(reviewId), root, requested.Intent);
        var acquire = await host.CommandAsync(started, nameof(AuthorizeProviderAssuranceReportProviderReviewUse), "acquire");
        Assert.True((await host.ReceiveAsync(acquire)).IsSuccess);

        // Act
        var held = await host.SendAsync(new ReviseAssuranceReport(Uuid.CreateVersion4(), host.Tenant, host.ProviderId,
            report.ReportId, 1, Report() with { Opinion = "qualified" }));
        var process = await host.ResumeAsync(await host.ReviewKey(reviewId));
        var released = await host.SendAsync(new ReviseAssuranceReport(Uuid.CreateVersion4(), host.Tenant, host.ProviderId,
            report.ReportId, 1, Report() with { Opinion = "qualified" }));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, held.Error?.Kind);
        Assert.True(held.Error!.IsTransient);
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.True(released.IsSuccess, released.Error?.Message);
        Assert.Equal(2, released.Value.Revision);
    }
}
