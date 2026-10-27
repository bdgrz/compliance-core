using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.Snapshots.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
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
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Snapshots.Aggregates;
using Bdgrz.Compliance.Features.Snapshots.Events;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Commands;
namespace Bdgrz.Compliance.Tests.Features.Snapshots.Tests;

public sealed class PopulationSnapshotChunkingTests
{
    const string Kind = "access_population";
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Fact]
    public async Task ShouldFreezeAndReadBackEveryRowGivenPopulationBeyondInlineBound()
    {
        // Arrange
        await using var harness = Harness.Create();
        var rows = Rows(5_000);

        // Act
        var frozen = await harness.FreezeAsync(rows);
        var read = await harness.ReadAsync(frozen.Value.SnapshotId);
        var chunkEvents = await harness.ChunkEventsAsync();

        // Assert
        Assert.True(frozen.IsSuccess, frozen.IsSuccess ? null : frozen.Error.Message);
        Assert.True(read.IsSuccess, read.IsSuccess ? null : read.Error.Message);
        Assert.Equal(5_000, read.Value.Rows.Count);
        Assert.Equal(rows.Select(row => row.Key), read.Value.Rows.Select(row => row.Key));
        Assert.Equal(PopulationContentIdentity.Compute(Kind, rows).Value.Sha256,
            read.Value.Snapshot.ContentSha256);
        Assert.True(read.Value.Snapshot.StoredChunkCount > 1);
        Assert.Equal(read.Value.Snapshot.StoredChunkCount, chunkEvents.Count);
        Assert.All(chunkEvents, chunk => Assert.True(
            JsonSerializer.SerializeToUtf8Bytes(chunk,
                ComplianceCoreJsonContext.Default.PopulationSnapshotChunkStored).Length <
            PopulationSnapshotStorage.ChunkByteBudget + 1024,
            "Each stored chunk event must stay well inside the Fitz 16-bit operation payload."));
    }

    [Fact]
    public async Task ShouldRegenerateIdenticalManifestGivenRepeatedRegenerationAndEquivalentAmendment()
    {
        // Arrange
        await using var harness = Harness.Create();
        var rows = Rows(1_500);
        var original = (await harness.FreezeAsync(rows)).Value;
        var amendment = (await harness.FreezeAsync(rows, original.SnapshotId, "Metadata corrected")).Value;

        // Act
        var first = await harness.RegenerateAsync(original.SnapshotId);
        var second = await harness.RegenerateAsync(original.SnapshotId);
        var amended = await harness.RegenerateAsync(amendment.SnapshotId);

        // Assert
        Assert.True(first.IsSuccess, first.IsSuccess ? null : first.Error.Message);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(original.ContentSha256, first.Value.ContentSha256);
        Assert.Equal(first.Value.ContentSha256, amended.Value.ContentSha256);
        Assert.NotEqual(first.Value.ManifestSha256, amended.Value.ManifestSha256);
        Assert.Equal(1_500, first.Value.RowCount);
        Assert.StartsWith("{\"format_version\":1,\"kind\":\"access_population\"",
            first.Value.CanonicalManifest, StringComparison.Ordinal);
        Assert.Contains($"\"amends_snapshot_id\":\"{original.SnapshotId}\"",
            amended.Value.CanonicalManifest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldFailIntegrityGivenMissingStoredChunk()
    {
        // Arrange
        await using var harness = Harness.Create();
        var frozen = (await harness.FreezeAsync(Rows(3_000))).Value;
        harness.HideChunk(1);

        // Act
        var read = await harness.ReadAsync(frozen.SnapshotId);
        var regenerated = await harness.RegenerateAsync(frozen.SnapshotId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(read.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(regenerated.Error).Kind);
    }

    [Fact]
    public async Task ShouldCompleteOneSnapshotGivenRetryAfterChunksStoredWithoutManifest()
    {
        // Arrange
        await using var harness = Harness.Create();
        var rows = Rows(2_000);
        var requestId = Uuid.CreateVersion4();
        harness.FailManifestOnce();
        var interrupted = await Record.ExceptionAsync(async () =>
            await harness.FreezeAsync(rows, requestId: requestId));
        var invisible = await harness.ReadAsync(requestId);

        // Act
        var retried = await harness.FreezeAsync(rows, requestId: requestId);
        var read = await harness.ReadAsync(requestId);

        // Assert
        Assert.NotNull(interrupted);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(invisible.Error).Kind);
        Assert.True(retried.IsSuccess, retried.IsSuccess ? null : retried.Error.Message);
        Assert.Equal(2_000, read.Value.Rows.Count);
        Assert.Equal(read.Value.Snapshot.StoredChunkCount, (await harness.ChunkEventsAsync()).Count);
    }

    [Fact]
    public async Task ShouldRejectFreezeWithoutWritingGivenRowBeyondChunkBudget()
    {
        // Arrange
        await using var harness = Harness.Create();
        PopulationRow[] rows = [Row("a", new string('x', PopulationSnapshotStorage.ChunkByteBudget))];

        // Act
        var frozen = await harness.FreezeAsync(rows);

        // Assert
        var error = Assert.IsType<RequestError>(frozen.Error);
        Assert.Equal(RequestErrorKind.Validation, error.Kind);
        Assert.Contains("row 1", error.Message, StringComparison.Ordinal);
        Assert.Empty(await harness.ChunkEventsAsync());
    }

    [Fact]
    public void ShouldPlanBoundedChunksQuicklyGivenMaximumPopulation()
    {
        // Arrange
        var rows = Rows((int)PopulationContentIdentity.MaximumRows);

        // Act
        var started = Stopwatch.GetTimestamp();
        var plan = PopulationSnapshotStorage.Plan(Kind, rows);
        var elapsed = Stopwatch.GetElapsedTime(started);

        // Assert
        Assert.True(plan.IsSuccess, plan.IsSuccess ? null : plan.Error.Message);
        Assert.Equal(rows.Length, plan.Value.Sum(chunk => chunk.Count));
        Assert.All(plan.Value, chunk => Assert.True(chunk.EncodedBytes <= PopulationSnapshotStorage.ChunkByteBudget));
        Assert.True(plan.Value.Count <= PopulationSnapshotStorage.MaximumChunks);
        Assert.True(elapsed < TimeSpan.FromSeconds(30), $"Planning 250k rows took {elapsed}.");
    }

    [Fact]
    public void ShouldKeepManifestIdentityGivenSamePlan()
    {
        // Arrange
        var rows = Rows(4_000);

        // Act
        var first = PopulationSnapshotStorage.Plan(Kind, rows).Value;
        var second = PopulationSnapshotStorage.Plan(Kind, [.. rows]).Value;

        // Assert
        Assert.Equal(PopulationSnapshotStorage.ManifestSha256(Kind, rows.Length, first),
            PopulationSnapshotStorage.ManifestSha256(Kind, rows.Length, second));
        Assert.Equal(first.Select(chunk => chunk.Sha256), second.Select(chunk => chunk.Sha256));
    }

    static PopulationRow[] Rows(int count)
    {
        var rows = new PopulationRow[count];
        for (var index = 0; index < count; index++)
            rows[index] = Row($"account/{index.ToString("D6", CultureInfo.InvariantCulture)}",
                $"user-{index}@example.com");
        return rows;
    }

    static PopulationRow Row(string key, string email)
    {
        using var document = JsonDocument.Parse($$"""{"email":"{{email}}","status":"active"}""");
        return new PopulationRow(key, document.RootElement.Clone());
    }

    sealed class Harness : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly HidingReader _reader;
        readonly FailingWriter _writer;
        readonly Uuid _tenantId = Uuid.CreateVersion4();

        Harness(ServiceProvider provider)
        {
            _provider = provider;
            _reader = new HidingReader(provider.GetRequiredService<IAggregateReader>());
            _writer = new FailingWriter(provider.GetRequiredService<IAggregateWriter>());
        }

        public static Harness Create()
        {
            var services = new ServiceCollection();
            var store = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.AddSingleton(TimeProvider.System);
            services.AddPortia();
            return new Harness(services.BuildServiceProvider());
        }

        public void HideChunk(int index) => _reader.HiddenChunk = index;

        public void FailManifestOnce() => _writer.FailNextManifest = true;

        public async Task<Result<SnapshotRegistration>> FreezeAsync(IReadOnlyList<PopulationRow> rows,
            Uuid? amends = null, string? reason = null, Uuid? requestId = null)
        {
            var context = new RequestContext<FreezeWorkforceRosterSnapshot>(
                new FreezeWorkforceRosterSnapshot(_tenantId), Actor(),
                requestId: requestId ?? Uuid.CreateVersion4());
            var freezer = new PopulationSnapshotFreezer(_reader,
                new AggregateExecutor(_reader, _writer), TimeProvider.System);
            return await freezer.FreezeAsync(context, _tenantId, Kind, rows, amends, reason, Author,
                CancellationToken.None);
        }

        public ValueTask<Result<PopulationSnapshotContent>> ReadAsync(Uuid snapshotId) =>
            PopulationSnapshotContent.ReadAsync(_reader, _tenantId, snapshotId, Kind,
                CancellationToken.None);

        public ValueTask<Result<PopulationSnapshotManifestRegeneration>> RegenerateAsync(
            Uuid snapshotId) => PopulationSnapshotManifest.RegenerateAsync(_reader, _tenantId,
            snapshotId, Kind, CancellationToken.None);

        public async Task<IReadOnlyList<PopulationSnapshotChunkStored>> ChunkEventsAsync()
        {
            var events = new List<PopulationSnapshotChunkStored>();
            await foreach (var record in _provider.GetRequiredService<IDomainEventReader>().ReadAsync(
                               EventStreamPattern.ForPattern(_tenantId.ToString(),
                                   PopulationSnapshotChunk.Category), EventCursor.Start, CancellationToken.None))
                if (record.Event is PopulationSnapshotChunkStored chunk)
                    events.Add(chunk);
            return events;
        }

        public ValueTask DisposeAsync() => _provider.DisposeAsync();

        static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
            "BdgrzSession"));
    }

    sealed class HidingReader(IAggregateReader inner) : IAggregateReader
    {
        public int? HiddenChunk { get; set; }

        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            aggregate is PopulationSnapshotChunk chunk && chunk.Index == HiddenChunk
                ? ValueTask.FromResult(aggregate)
                : inner.HydrateAsync(aggregate, ct);
    }

    sealed class FailingWriter(IAggregateWriter inner) : IAggregateWriter
    {
        public bool FailNextManifest { get; set; }

        public ValueTask SaveAsync<TAggregate>(TAggregate aggregate, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (aggregate is PopulationSnapshot && FailNextManifest)
            {
                FailNextManifest = false;
                throw new IOException("Simulated interruption before the manifest commit.");
            }
            return inner.SaveAsync(aggregate, context, ct);
        }
    }
}
