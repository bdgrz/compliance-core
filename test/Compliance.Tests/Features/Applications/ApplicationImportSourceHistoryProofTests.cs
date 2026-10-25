using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportSourceHistoryProofTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectUnselectedIntermediateSubstitutionGivenActualHydratedSourceCut(bool metadataOnly)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        for (var index = 0; index < 3; index++)
            await FenceAsync(host, "prefix", index);
        var owner = await host.SourceAsync("prefix");
        var facts = new List<DomainEvent>();
        await foreach (var record in host.Events.ReadAsync(owner.Stream, 0))
            facts.Add(record.Event);
        var original = Assert.IsType<ApplicationImportSourceSettled>(facts[1]);
        var changed = original with
        {
            Intent = metadataOnly ? original.Intent : original.Intent with { ContentId = Uuid.CreateVersion4() },
        };
        changed.AttachMetadata(metadataOnly ? original.Metadata with { EventId = Uuid.CreateVersion4() } : original.Metadata);
        facts[1] = changed;

        // Act
        var error = await Record.ExceptionAsync(async () => _ = await ApplicationImportSourceHistory.ReadAsync(
            new Records(owner.Stream, facts.ToArray()), owner));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(error);
        Assert.Equal(3, owner.Revision);
        Assert.Null(owner.ActiveBatchId);
    }

    [Fact]
    public async Task ShouldKeepSelectedEarlierFenceGivenLaterFencedBatchAndActiveOtherHolder()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var first = await FenceAsync(host, "selected", 1);
        var second = await FenceAsync(host, "selected", 2);
        var held = await host.StageBatchAsync(key: "selected");
        await host.SealPlanAsync(held.Id, [ApplicationImportHost.Create(Uuid.CreateVersion4(), "held")]);
        var process = await host.StartProcessAsync(held.Id);
        var claim = await host.SendAsync((await host.StepAsync(process, "claim", nameof(ClaimApplicationImportSource), process.Targets[0]))!);
        Assert.True(claim.IsSuccess, claim.Error?.Message);

        // Act
        var source = await host.SourceAsync("selected", selectedBatchId: first);
        var history = await host.SourceHistoryAsync(source);

        // Assert
        Assert.True(source.Find(first)!.IsFence);
        Assert.Null(source.Find(second));
        Assert.True(source.Find(held.Id)!.IsActive);
        Assert.Equal(held.Id, source.ActiveBatchId);
        Assert.True(history.Find(second)!.IsFence);
        Assert.Empty(history.Published);
        Assert.Equal(3, source.Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldClassifyExactPublicationBytesGivenMissingOrCorruptRows(bool missing)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync(key: "published");
        var row = Assert.Single(await host.RowsAsync(batch));
        Assert.True((await host.CorrelateAsync(batch.Id, row.RowId, 1, "create_new")).IsSuccess);
        Assert.True((await host.AcceptAsync(batch.Id, 2)).IsSuccess);
        await host.RunProcessAsync(batch.Id);
        var store = new FailedRows(host.Content, missing);

        // Act
        var read = await ApplicationImportSourceView.LoadAsync(host.Reader, store, host.Events, host.TenantId,
            "published", "applications");

        // Assert
        Assert.False(read.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, read.Error!.Kind);
        Assert.Equal(missing, read.Error.IsTransient);
        Assert.Equal(2, (await host.SourceAsync("published")).Revision);
    }

    [Fact]
    public async Task ShouldRefuseForgedPlanTriggerGivenSameNativeIdentityAndCommittedDifferentPayload()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(Uuid.CreateVersion4(), "one")]);
        DomainEventRecord? stored = null;
        await foreach (var record in host.Events.ReadAsync(batch.Stream, 0))
            if (record.Event is ApplicationImportPlanSealed)
                stored = record;
        var committed = stored ?? throw new InvalidOperationException("The committed plan was not found.");
        var original = Assert.IsType<ApplicationImportPlanSealed>(committed.Event);
        var forged = original with { TargetGroupCount = original.TargetGroupCount + 1 };
        forged.AttachMetadata(original.Metadata);
        var reactor = host.Services.GetRequiredService<ImportSourceAdmissionReactor>();
        await new ReactorScenario(new TenantId(host.TenantId.ToString())).RunAsync(reactor);
        var source = new DomainEventRecord(committed.Stream, forged, committed.ResourceOffset, committed.NextCursor);

        // Act
        var error = await Record.ExceptionAsync(async () => await reactor.HandleAsync(
            AdministratorTransitionHost.ReactionOf<ApplicationImportPlanSealed>(source), CancellationToken.None));

        // Assert
        var refused = Assert.IsType<ReactionCommandFailedException>(error);
        Assert.False(refused.Error.IsTransient);
        Assert.Equal(0, (await host.SourceAsync()).Revision);
        Assert.Null((await host.BatchAsync(batch.Id)).Committed);
    }

    static async Task<Uuid> FenceAsync(ApplicationImportHost host, string key, int index)
    {
        var batch = await host.StageBatchAsync(key: key);
        await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(Uuid.CreateVersion4(), $"record-{index}")]);
        Assert.True((await host.CancelAsync(batch.Id, 2)).IsSuccess);
        await host.ReactCancellationAsync(batch.Id);
        return batch.Id;
    }

    sealed class FailedRows(IImmutableContentStore inner, bool missing) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => reference.Schema == "import-planned-row.v1"
            ? throw (missing ? new ImmutableContentUnavailableException("The exact published rows are temporarily absent.")
                : new ImmutableContentIntegrityException("The exact published rows failed verification."))
            : inner.ReadVerifiedAsync(realm, reference, ct);
    }

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset,
            CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct = default) => Enumerate(stream, 0, ct);
        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (address != stream)
                yield break;
            for (var index = fromOffset; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], index, EventCursor.Start);
            }
        }
    }
}
