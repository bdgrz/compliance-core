using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderAssuranceGapTests
{
    [Fact]
    public async Task ShouldMaterializeAcceptedRiskLinkGivenGapClosesAfterAdmission()
    {
        // Arrange: an open gap at revision 6 and an exact active risk acceptance.
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var gap = await host.RecordGapAsync();
        var revision = gap.Revision;
        for (var index = 0; index < 4; index++)
        {
            var other = await host.AcceptRiskAsync();
            var admitted = await host.LinkRequestAsync(gap.GapId, revision, other.RiskId, other.AcceptanceId);
            Assert.True(admitted.IsSuccess, admitted.Error?.Message);
            revision++;
        }
        Assert.Equal(5, revision);
        var filler = await host.AcceptRiskAsync();
        Assert.True((await host.LinkRequestAsync(gap.GapId, revision, filler.RiskId, filler.AcceptanceId)).IsSuccess);
        revision++;
        Assert.Equal(6, revision);
        var accepted = await host.AcceptRiskAsync();
        var linkId = Uuid.CreateVersion4();

        // Act 1: link under L at ExpectedGapRevision 6 admits and freezes the admission revision.
        var admission = await host.LinkRequestAsync(gap.GapId, 6, accepted.RiskId, accepted.AcceptanceId, linkId);
        var afterAdmission = await host.GapAsync(gap.GapId);

        // Act 2: close the gap with a permitted newer source before the child materializes.
        var report = await host.ReviseReportAsync((await FirstReportAsync(host, afterAdmission)).ReportId, 1);
        var closed = await host.CloseGapAsync(gap.GapId, 7, new ProviderCoverageGapClosureContent("coverage_restored",
            "assurance_report", report.ReportId, report.Revision, "A newer report covers the service."));
        var afterClose = await host.GapAsync(gap.GapId);
        var late = await host.AcceptRiskAsync();
        var rejectedAfterClose = await host.LinkRequestAsync(gap.GapId, 8, late.RiskId, late.AcceptanceId);
        var gapFactsBeforeChild = (await host.FactsAsync(ProviderAssuranceStreams.Gap(host.Tenant, host.ProviderId, gap.GapId))).Count;

        // Act 3: materialize L and retry the original command.
        var process = await host.DriveLinkAsync(linkId, gap.GapId);
        var retry = await host.LinkRequestAsync(gap.GapId, 6, accepted.RiskId, accepted.AcceptanceId, linkId);
        var link = await host.LinkAsync(linkId);
        var linkFacts = await host.FactsAsync(ProviderAssuranceStreams.RiskAcceptance(host.Tenant, linkId));
        var afterChild = await host.GapAsync(gap.GapId);

        // Assert
        Assert.True(admission.IsSuccess, admission.Error?.Message);
        Assert.Equal(7, afterAdmission.Revision);
        Assert.True(closed.IsSuccess, closed.Error?.Message);
        Assert.Equal(8, afterClose.Revision);
        Assert.Equal(RequestErrorKind.Conflict, rejectedAfterClose.Error?.Kind);
        Assert.Equal(ProcessState.Completed, process.State);
        var linked = Assert.IsType<ProviderCoverageGapRiskAcceptanceLinked>(Assert.Single(linkFacts));
        Assert.Equal(linkId, linked.RequestId);
        Assert.Equal(7, linked.Revision);
        Assert.Equal(linkId, linked.Operation.RootOperationId);
        Assert.Same(linked, link.Linked);
        Assert.Equal(admission.Value, retry.Value);
        Assert.Equal(linkId, retry.Value.OperationId);
        var result = await host.Content.ReadJsonAsync<ProviderCoverageGapRegistration>(host.Tenant.ToString(), linked.OperationResult!);
        Assert.Equal(new ProviderCoverageGapRegistration(gap.GapId, 7), result);
        Assert.False(afterChild.IsOpen);
        Assert.Equal(gapFactsBeforeChild, (await host.FactsAsync(ProviderAssuranceStreams.Gap(host.Tenant, host.ProviderId, gap.GapId))).Count);
        var view = await host.Content.ReadJsonAsync<ProviderCoverageGapRiskAcceptanceView>(host.Tenant.ToString(), linked.AcceptanceContent);
        Assert.Equal(accepted.AcceptanceId, view.AcceptanceId);
        Assert.Equal(accepted.ExpiresAt, view.ExpiresAt);
        Assert.Equal(7, view.Revision);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactRetryAfterLaterOwnerChange()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var gapId = Uuid.CreateVersion4();
        var content = await host.GapContentAsync();
        var first = await host.RecordGapAsync(gapId, content);
        var report = await host.ReviseReportAsync(content.SourceId, 1);
        var close = new ProviderCoverageGapClosureContent("coverage_restored", "assurance_report", report.ReportId,
            report.Revision, "A newer report covers the service.");
        var commandId = Uuid.CreateVersion4();
        var closed = await host.CloseGapAsync(gapId, 1, close, commandId);
        Assert.True(closed.IsSuccess, closed.Error?.Message);

        // Act
        var recordRetry = await host.SendAsync(new RecordProviderCoverageGap(gapId, host.Tenant, host.ProviderId, content));
        var closeRetry = await host.SendAsync(new CloseProviderCoverageGap(commandId, host.Tenant, host.ProviderId, gapId, 1, close));

        // Assert
        Assert.Equal(first, recordRetry.Value);
        Assert.Equal(1, recordRetry.Value.Revision);
        Assert.Equal(closed.Value, closeRetry.Value);
        Assert.Equal(2, closeRetry.Value.Revision);
        Assert.Equal(2, (await host.FactsAsync(ProviderAssuranceStreams.Gap(host.Tenant, host.ProviderId, gapId))).Count);
    }

    [Fact]
    public async Task ShouldConflictGivenSameIdentityWithChangedActorIntentEpisodeOrOriginalRevision()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var gapId = Uuid.CreateVersion4();
        var content = await host.GapContentAsync();
        await host.RecordGapAsync(gapId, content);
        var accepted = await host.AcceptRiskAsync();
        var other = await host.AcceptRiskAsync();
        var linkId = Uuid.CreateVersion4();
        Assert.True((await host.LinkRequestAsync(gapId, 1, accepted.RiskId, accepted.AcceptanceId, linkId)).IsSuccess);

        // Act
        var changedGap = await host.SendAsync(new RecordProviderCoverageGap(gapId, host.Tenant, host.ProviderId,
            content with { Description = "A different description." }));
        var changedActor = await host.SendAsync(new RecordProviderCoverageGap(gapId, host.Tenant, host.ProviderId, content),
            user: Uuid.CreateVersion4());
        var changedPair = await host.LinkRequestAsync(gapId, 1, other.RiskId, other.AcceptanceId, linkId);
        var changedExpected = await host.LinkRequestAsync(gapId, 2, accepted.RiskId, accepted.AcceptanceId, linkId);
        var changedLinkActor = await host.SendAsync(new LinkProviderCoverageGapRiskAcceptance(linkId, host.Tenant,
            ProviderAssuranceHost.ProgramId, host.ProviderId, gapId, 1, accepted.RiskId, accepted.AcceptanceId),
            user: Uuid.CreateVersion4());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changedGap.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedActor.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedPair.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedExpected.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedLinkActor.Error?.Kind);
        Assert.Equal(2, (await host.FactsAsync(ProviderAssuranceStreams.Gap(host.Tenant, host.ProviderId, gapId))).Count);
    }

    [Fact]
    public async Task ShouldRejectClosureWithoutANewSourceRevisionGivenAnOpenGap()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var content = await host.GapContentAsync();
        var gap = await host.RecordGapAsync(content: content);

        // Act
        var same = await host.CloseGapAsync(gap.GapId, 1, new ProviderCoverageGapClosureContent("coverage_restored",
            content.SourceKind, content.SourceId, content.SourceRevision, "No new evidence."));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, same.Error?.Kind);
        Assert.True((await host.GapAsync(gap.GapId)).IsOpen);
    }

    [Fact]
    public async Task ShouldCloseOnceGivenNewerSourceAndRejectASecondClosure()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var content = await host.GapContentAsync();
        var gap = await host.RecordGapAsync(content: content);
        var revised = await host.ReviseReportAsync(content.SourceId, 1);
        var close = new ProviderCoverageGapClosureContent("coverage_restored", "assurance_report", revised.ReportId,
            revised.Revision, "A newer report covers the service.");

        // Act
        var closed = await host.CloseGapAsync(gap.GapId, 1, close);
        var again = await host.CloseGapAsync(gap.GapId, 2, close with { Rationale = "Second closure." });
        var stale = await host.CloseGapAsync(gap.GapId, 1, close with { Rationale = "Stale." });

        // Assert
        Assert.Equal(2, closed.Value.Revision);
        Assert.Equal(RequestErrorKind.Conflict, again.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.False((await host.GapAsync(gap.GapId)).IsOpen);
    }

    [Fact]
    public async Task ShouldRejectLinkGivenStaleRevisionDuplicatePairOrExpiredAcceptance()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var gap = await host.RecordGapAsync();
        var accepted = await host.AcceptRiskAsync();
        var expired = await host.AcceptRiskAsync(TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);
        Assert.True((await host.LinkRequestAsync(gap.GapId, 1, accepted.RiskId, accepted.AcceptanceId)).IsSuccess);

        // Act
        var stale = await host.LinkRequestAsync(gap.GapId, 1, (await host.AcceptRiskAsync()).RiskId, Uuid.CreateVersion4());
        var duplicate = await host.LinkRequestAsync(gap.GapId, 2, accepted.RiskId, accepted.AcceptanceId);
        var lapsed = await host.LinkRequestAsync(gap.GapId, 2, expired.RiskId, expired.AcceptanceId);
        var unknown = await host.LinkRequestAsync(gap.GapId, 2, Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, duplicate.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, lapsed.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error?.Kind);
        Assert.Equal(2, (await host.GapAsync(gap.GapId)).Revision);
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenWrongOrStaleSourceProof()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var gap = await host.RecordGapAsync();
        var accepted = await host.AcceptRiskAsync();
        var linkId = Uuid.CreateVersion4();
        Assert.True((await host.LinkRequestAsync(gap.GapId, 1, accepted.RiskId, accepted.AcceptanceId, linkId)).IsSuccess);
        var root = await host.RootAsync(ProviderAssuranceStreams.Gap(host.Tenant, host.ProviderId, gap.GapId), linkId);
        var admitted = (ProviderCoverageGapRiskAcceptanceAdmitted)root.Event;
        var record = await host.PrepareProcessAsync(await host.LinkKey(linkId), root, admitted.Intent);
        var good = (MaterializeProviderRiskAcceptanceLink)await host.CommandAsync(record,
            nameof(MaterializeProviderRiskAcceptanceLink), "materialize");

        // Act
        var wrongSource = await host.ReceiveAsync(good with { Source = good.Source with { EventId = Uuid.CreateVersion4() } });
        var wrongIntent = await host.ReceiveAsync(good with { Intent = admitted.Intent with { Sha256 = new string('0', 64) } });
        var wrongTarget = await host.ReceiveAsync(good with
        {
            Target = ProviderAssuranceStreams.RiskAcceptance(host.Tenant, Uuid.CreateVersion4()),
        });
        var notSystem = await host.ReceiveAsync(good, ProgramManagementServices.Actor(host.User));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, wrongSource.Error?.Kind);
        Assert.False(wrongSource.Error!.IsTransient);
        Assert.False(wrongIntent.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, wrongTarget.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, notSystem.Error?.Kind);
        Assert.Empty(await host.FactsAsync(ProviderAssuranceStreams.RiskAcceptance(host.Tenant, linkId)));
    }

    [Fact]
    public async Task ShouldReplayWithoutDuplicateAppendGivenMaterializationRetriedAfterCrash()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var gap = await host.RecordGapAsync();
        var accepted = await host.AcceptRiskAsync();
        var linkId = Uuid.CreateVersion4();
        Assert.True((await host.LinkRequestAsync(gap.GapId, 1, accepted.RiskId, accepted.AcceptanceId, linkId)).IsSuccess);
        var root = await host.RootAsync(ProviderAssuranceStreams.Gap(host.Tenant, host.ProviderId, gap.GapId), linkId);
        var admitted = (ProviderCoverageGapRiskAcceptanceAdmitted)root.Event;
        var record = await host.PrepareProcessAsync(await host.LinkKey(linkId), root, admitted.Intent);
        var command = await host.CommandAsync(record, nameof(MaterializeProviderRiskAcceptanceLink), "materialize");
        var appended = await host.ReceiveAsync(command);

        // Act: the worker crashed after the receiver appended and before the process record advanced.
        var identical = await host.CommandAsync(record, nameof(MaterializeProviderRiskAcceptanceLink), "materialize");
        var replay = await host.ReceiveAsync(identical);
        var resumed = await host.ResumeAsync(await host.LinkKey(linkId));

        // Assert
        Assert.Equal(command, identical);
        Assert.IsType<ReceiverOutcome.Applied>(appended.Value);
        Assert.IsType<ReceiverOutcome.Replayed>(replay.Value);
        Assert.Single(await host.FactsAsync(ProviderAssuranceStreams.RiskAcceptance(host.Tenant, linkId)));
        Assert.Equal(ProcessState.Completed, resumed.State);
    }

    [Fact]
    public async Task ShouldProjectLinkOnGapWithoutChangingStatusGivenMaterializedLink()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var gap = await host.RecordGapAsync();
        var accepted = await host.AcceptRiskAsync();
        var linkId = Uuid.CreateVersion4();
        Assert.True((await host.LinkRequestAsync(gap.GapId, 1, accepted.RiskId, accepted.AcceptanceId, linkId)).IsSuccess);
        await host.DriveLinkAsync(linkId, gap.GapId);

        // Act
        await host.CatchUpAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var view = await scope.ServiceProvider.GetRequiredService<IAssuranceReader>()
            .GetCoverageGapAsync(host.Tenant, host.ProviderId, gap.GapId);

        // Assert
        Assert.Equal("open", view!.Status);
        Assert.Equal(2, view.Revision);
        var link = Assert.Single(view.RiskAcceptances);
        Assert.Equal(accepted.AcceptanceId, link.AcceptanceId);
        Assert.Equal(2, link.Revision);
    }

    static async Task<AssuranceReportRegistration> FirstReportAsync(ProviderAssuranceHost host, ProviderAssuranceGap gap)
    {
        var content = await ProviderAssuranceContentReader.ReadGapAsync(host, gap);
        return new AssuranceReportRegistration(content.SourceId, content.SourceRevision);
    }
}

static class ProviderAssuranceContentReader
{
    public static Task<ProviderCoverageGapContent> ReadGapAsync(ProviderAssuranceHost host, ProviderAssuranceGap gap) =>
        host.Content.ReadJsonAsync<ProviderCoverageGapContent>(host.Tenant.ToString(), gap.Recorded!.ContentContent);
}
