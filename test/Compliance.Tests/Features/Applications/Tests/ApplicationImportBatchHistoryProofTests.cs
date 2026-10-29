using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.History;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
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
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

public sealed class ApplicationImportBatchHistoryProofTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectIntermediateSubstitutionGivenSameFinalBatchVersionAndHead(bool metadataOnly)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var row = Assert.Single(await host.RowsAsync(batch));
        Assert.True((await host.CorrelateAsync(batch.Id, row.RowId, 1, "create_new")).IsSuccess);
        Assert.True((await host.CorrelateAsync(batch.Id, row.RowId, 2, "create_new")).IsSuccess);
        var owner = await host.BatchAsync(batch.Id);
        var facts = new List<DomainEvent>();
        await foreach (var record in host.Events.ReadAsync(owner.Stream, 0))
            facts.Add(record.Event);
        var original = Assert.IsType<ApplicationImportRowCorrelated>(facts[1]);
        var changed = original with { Reason = metadataOnly ? original.Reason : "A different coherent earlier justification." };
        changed.AttachMetadata(metadataOnly ? original.Metadata with { EventId = Uuid.CreateVersion4() } : original.Metadata);
        facts[1] = changed;

        // Act
        var error = await Record.ExceptionAsync(async () => _ = await ApplicationImportBatchHistory.ReadAsync(
            new Records(owner.Stream, facts.ToArray()), owner));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(error);
        Assert.Equal(3, owner.Revision);
    }

    [Fact]
    public async Task ShouldKeepCompleteExternalChoicesGivenManyDistinctRowsWithoutRetainingThemInAuthority()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var input = Enumerable.Range(1, 24).Select(index => new ApplicationImportInputRow($"record-{index}",
            $"Application {index}", "The observed purpose.", null)).ToArray();
        var batch = await host.StageBatchAsync(rows: input);
        var rows = await host.RowsAsync(batch);
        for (var index = 0; index < rows.Count; index++)
            Assert.True((await host.CorrelateAsync(batch.Id, rows[index].RowId, index + 1, "create_new")).IsSuccess);
        var owner = await host.BatchAsync(batch.Id);

        // Act
        var history = await host.BatchHistoryAsync(owner);
        var retained = typeof(ApplicationImportBatch).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(owner) is ICollection { Count: > 1 }).ToArray();
        var verified = await ApplicationImportBatchRows.ReadAsync(host.Content, owner, history, CancellationToken.None);

        // Assert
        Assert.Empty(retained);
        Assert.Equal(24, history.Correlations.Count);
        Assert.Equal(24, verified.Count);
        Assert.All(rows, row => Assert.Equal(row.SourceRecordId, history.GetCorrelation(row.RowId)!.SourceRecordId));
        Assert.Null(owner.GetCorrelation(rows[0].RowId));
        Assert.Equal(25, owner.Revision);
    }

    [Fact]
    public async Task ShouldPreserveCapturedOlderBatchCutGivenLegitimateLaterCorrection()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var row = Assert.Single(await host.RowsAsync(batch));
        Assert.True((await host.CorrelateAsync(batch.Id, row.RowId, 1, "create_new")).IsSuccess);
        var captured = await host.BatchAsync(batch.Id);
        Assert.True((await host.CorrelateAsync(batch.Id, row.RowId, 2, "create_new")).IsSuccess);

        // Act
        var prior = await host.BatchHistoryAsync(captured);
        var current = await host.BatchHistoryAsync(await host.BatchAsync(batch.Id));

        // Assert
        Assert.Equal(2, prior.Facts.Count);
        Assert.Equal(2, prior.GetCorrelation(row.RowId)!.Revision);
        Assert.Equal(3, current.Facts.Count);
        Assert.Equal(3, current.GetCorrelation(row.RowId)!.Revision);
        Assert.True(prior.Matches(captured));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldFreezeOriginalDeclaredUnitEnvelopeGivenCommittedOrCanceledTerminalFact(bool cancel)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var row = Assert.Single(await host.RowsAsync(batch));
        Assert.True((await host.CorrelateAsync(batch.Id, row.RowId, 1, "create_new")).IsSuccess);
        Assert.True((await host.AcceptAsync(batch.Id, 2)).IsSuccess);
        if (cancel)
        {
            Assert.True((await host.CancelAsync(batch.Id, 3)).IsSuccess);
            await host.ReactCancellationAsync(batch.Id);
        }
        else
            await host.RunProcessAsync(batch.Id);
        var saved = await host.BatchAsync(batch.Id);
        var terminalResult = cancel ? saved.Canceled!.OperationResult : saved.Committed!.OperationResult;

        // Act
        var result = await ApplicationImportContent.ReadAsync<MutationResultContent>(host.Content, host.TenantId,
            terminalResult!, CancellationToken.None);
        var body = Assert.IsType<StandardResponseBody>(result.Value);
        var original = await ApplicationImportContent.ReadAsync<UnitResult>(host.Content, host.TenantId,
            body.Content, CancellationToken.None);

        // Assert
        Assert.NotNull(terminalResult);
        Assert.Equal(UnitResult.Schema, result.Schema);
        Assert.Equal(new UnitResult(), original);
        Assert.Equal(5, saved.Revision);
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
