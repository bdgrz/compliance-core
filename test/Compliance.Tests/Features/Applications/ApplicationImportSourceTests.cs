using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportSourceTests
{
    [Fact]
    public async Task ShouldAdmitOneBatchAtATimeGivenSealedBatchesOfOneSource()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var first = await host.StageBatchAsync(key: "shared");
        var second = await host.StageBatchAsync(key: "shared");
        await host.SealPlanAsync(first.Id, [ApplicationImportHost.Create(Uuid.CreateVersion4(), "one")]);
        await host.SealPlanAsync(second.Id, [ApplicationImportHost.Create(Uuid.CreateVersion4(), "two")]);
        var one = await host.StartProcessAsync(first.Id);
        var two = await host.StartProcessAsync(second.Id);

        // Act
        var claimed = await host.SendAsync((await host.StepAsync(one, "claim", nameof(ClaimApplicationImportSource),
            one.Targets[0]))!);
        var replay = await host.SendAsync((await host.StepAsync(one, "claim", nameof(ClaimApplicationImportSource),
            one.Targets[0]))!);
        var contested = await host.SendAsync((await host.StepAsync(two, "claim", nameof(ClaimApplicationImportSource),
            two.Targets[0]))!);
        var source = await host.SourceAsync("shared");

        // Assert
        Assert.IsType<ReceiverOutcome.Applied>(claimed.Value);
        Assert.IsType<ReceiverOutcome.Replayed>(replay.Value);
        Assert.Equal(RequestErrorKind.Conflict, contested.Error?.Kind);
        Assert.True(contested.Error!.IsTransient);
        Assert.Equal(first.Id, source.ActiveBatchId);
        Assert.Equal(1, source.Revision);
    }

    [Fact]
    public async Task ShouldRefuseClaimGivenSourceChangedAfterPlanFroze()
    {
        // Arrange: both batches froze against source revision 0; the first publishes and settles.
        await using var host = new ApplicationImportHost();
        var first = await host.StageBatchAsync(key: "shared");
        var stale = await host.StageBatchAsync(key: "shared");
        await host.SealPlanAsync(first.Id, [ApplicationImportHost.Create(Uuid.CreateVersion4(), "one")]);
        await host.SealPlanAsync(stale.Id, [ApplicationImportHost.Create(Uuid.CreateVersion4(), "two")]);
        Assert.Equal(ProcessState.Completed, (await host.RunProcessAsync(first.Id)).State);
        var record = await host.StartProcessAsync(stale.Id);

        // Act
        var claim = await host.SendAsync((await host.StepAsync(record, "claim", nameof(ClaimApplicationImportSource),
            record.Targets[0]))!);

        // Assert: the stale plan can never claim; the source is untouched.
        Assert.Equal(RequestErrorKind.Conflict, claim.Error?.Kind);
        Assert.False(claim.Error!.IsTransient);
        var source = await host.SourceAsync("shared");
        Assert.Null(source.ActiveBatchId);
        Assert.Equal(2, source.Revision);
    }

    [Fact]
    public async Task ShouldFenceLateClaimGivenSettlementBeforeAnyAdmission()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(Uuid.CreateVersion4(), "one")]);
        var record = await host.StartProcessAsync(batch.Id);
        var lateClaim = (await host.StepAsync(record, "claim", nameof(ClaimApplicationImportSource), record.Targets[0]))!;

        // Act: the cancellation settles the batch's source claim before it was ever granted.
        Assert.True((await host.CancelAsync(batch.Id, 2)).IsSuccess);
        await host.ReactCancellationAsync(batch.Id);
        var claim = await host.SendAsync(lateClaim);
        var source = await host.SourceAsync();

        // Assert
        Assert.IsType<ReceiverOutcome.Superseded>(claim.Value);
        var slot = (await host.SourceHistoryAsync(source)).Find(batch.Id)!;
        Assert.True(slot.IsFence);
        Assert.Equal(ApplicationImportDisposition.Canceled, slot.Settled!.Disposition);
        Assert.Null(source.ActiveBatchId);
        Assert.Empty((await host.SourceHistoryAsync(source)).Published);
    }

    [Fact]
    public async Task ShouldDeriveClaimsOnlyFromPublishedBatchesGivenCanceledLaterBatch()
    {
        // Arrange: record-1 is published, a later batch for the same source is canceled.
        await using var host = new ApplicationImportHost();
        var published = await host.StageBatchAsync(key: "derived");
        var publishedRow = (await host.RowsAsync(published)).Single().RowId;
        Assert.True((await host.CorrelateAsync(published.Id, publishedRow, 1, "create_new")).IsSuccess);
        Assert.True((await host.AcceptAsync(published.Id, 2)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await host.RunProcessAsync(published.Id)).State);
        var later = await host.StageBatchAsync(key: "derived", rows: new ApplicationImportInputRow("record-1", "Renamed",
            "Changed purpose", null));
        var laterRow = (await host.RowsAsync(later)).Single().RowId;
        var target = ApplicationImportEffects.CreatedApplicationId(published.Id, publishedRow);
        Assert.True((await host.CorrelateAsync(later.Id, laterRow, 1, "link_existing", target, 1)).IsSuccess);
        Assert.True((await host.AcceptAsync(later.Id, 2)).IsSuccess);
        var record = await host.StartProcessAsync(later.Id);
        Assert.True((await host.SendAsync((await host.StepAsync(record, "claim", nameof(ClaimApplicationImportSource),
            record.Targets[0]))!)).IsSuccess);
        Assert.True((await host.CancelAsync(later.Id, 3)).IsSuccess);
        await host.ReactCancellationAsync(later.Id);

        // Act
        var view = await ApplicationImportSourceView.LoadAsync(host.Reader, host.Content, host.Events, host.TenantId, "derived",
            "applications", ct: CancellationToken.None);
        var other = await ApplicationImportSourceView.LoadAsync(host.Reader, host.Content, host.Events, host.TenantId, "derived",
            "other-namespace", ct: CancellationToken.None);

        // Assert: the claim keeps the published observation and does not leak to another source identity.
        var claim = Assert.Single(view.Value!.Claims);
        Assert.Equal("Payroll", claim.Observation.Name);
        Assert.Equal(published.Id, claim.LastObservedBatchId);
        Assert.Equal(target, claim.Observation.ApplicationId);
        Assert.Empty(other.Value!.Claims);
        Assert.Equal([published.Id], view.Value.History.Published.Select(static fact => fact.BatchId));
        Assert.Equal(["name", "purpose"], claim.ChangedFields(
            (await host.RowsAsync(await host.BatchAsync(later.Id))).Single()));
    }

    [Fact]
    public void ShouldRejectSourceIdentityGivenUnboundedOrUntrimmedKeys()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();

        // Act
        var padded = Record.Exception(() => ApplicationImportSource.IdFor(tenant, " manual", "applications"));
        var long128 = Record.Exception(() => ApplicationImportSource.IdFor(tenant, new string('k', 129), "applications"));
        var ok = ApplicationImportSource.IdFor(tenant, "manual", "applications");

        // Assert
        Assert.IsType<ArgumentException>(padded);
        Assert.IsType<ArgumentException>(long128);
        Assert.Equal(ok, ApplicationImportSource.IdFor(tenant, "manual", "applications"));
        Assert.NotEqual(ok, ApplicationImportSource.IdFor(tenant, "manual", "other"));
    }
}
