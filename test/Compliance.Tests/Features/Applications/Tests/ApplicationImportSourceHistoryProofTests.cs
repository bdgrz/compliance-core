using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.History;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Reactors;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

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
