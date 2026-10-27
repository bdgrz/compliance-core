using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderGapHistoricalMaterializationTests
{
    [Fact]
    public async Task ShouldMaterializeOriginalCapturedAcceptanceGivenLaterGapClosureAndExpiredRisk()
    {
        // Arrange
        var clock = new Clock(DateTimeOffset.UtcNow);
        await using var host = await ProviderAssuranceHost.CreateAsync(clock: clock);
        var gap = await host.RecordGapAsync();
        var risk = await host.AcceptRiskAsync(TimeSpan.FromDays(1));
        clock.Set(DateTimeOffset.UtcNow);
        var linkId = Uuid.CreateVersion4();
        var first = await host.LinkRequestAsync(gap.GapId, 1, risk.RiskId, risk.AcceptanceId, linkId);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var owner = await host.GapAsync(gap.GapId);
        var recorded = await host.Content.ReadJsonAsync<ProviderCoverageGapContent>(host.Tenant.ToString(), owner.Recorded!.ContentContent);
        var source = await host.RootAsync(owner.Stream, linkId);
        var admitted = (ProviderCoverageGapRiskAcceptanceAdmitted)source.Event;
        var command = new MaterializeProviderRiskAcceptanceLink(Uuid.CreateVersion4(), NativeFactReference.Of(owner.Stream, admitted),
            ProviderAssuranceStreams.RiskAcceptance(host.Tenant, linkId), linkId, admitted.Generation, null, admitted.Intent, gap.GapId, linkId);
        clock.Set(clock.GetUtcNow().AddDays(2));
        var report = await host.ReviseReportAsync(recorded.SourceId, recorded.SourceRevision);
        var closed = await host.CloseGapAsync(gap.GapId, 2, new ProviderCoverageGapClosureContent("coverage_restored",
            "assurance_report", report.ReportId, report.Revision, "A newer report closes the gap."));
        Assert.True(closed.IsSuccess, closed.Error?.Message);

        // Act
        var materialized = await host.ReceiveAsync(command);
        var replay = await host.LinkRequestAsync(gap.GapId, 1, risk.RiskId, risk.AcceptanceId, linkId);

        // Assert
        Assert.True(clock.GetUtcNow() > risk.ExpiresAt);
        Assert.True(materialized.IsSuccess, materialized.Error?.Message);
        Assert.IsType<ReceiverOutcome.Applied>(materialized.Value);
        var linked = Assert.IsType<ProviderCoverageGapRiskAcceptanceLinked>(Assert.Single(await host.FactsAsync(command.Target)));
        var view = await host.Content.ReadJsonAsync<ProviderCoverageGapRiskAcceptanceView>(host.Tenant.ToString(), linked.AcceptanceContent);
        Assert.Equal(risk.RiskId, view.RiskId);
        Assert.Equal(risk.AcceptanceId, view.AcceptanceId);
        Assert.Equal(risk.ExpiresAt, view.ExpiresAt);
        Assert.Equal(first.Value.AcceptedAt, view.LinkedAt);
        Assert.Equal(2, view.Revision);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(3, (await host.GapAsync(gap.GapId)).Revision);
        Assert.False((await host.GapAsync(gap.GapId)).IsOpen);
    }

    sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Set(DateTimeOffset value) => _now = value;
    }
}
