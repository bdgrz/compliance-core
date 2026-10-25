using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Retention;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using AuditPopulationAggregate = Bdgrz.Compliance.Features.AuditPopulations.AuditPopulationRules;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.AuditPopulations;

public sealed partial class GenerateAuditPopulationExportHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldExportExactFrozenCsvGivenFrozenPopulationAndPersonalActor()
    {
        // Arrange
        await using var fixture = new Fixture(ArtifactInspectionState.Clean);
        var populationId = await fixture.SeedPopulationAsync(frozen: true);
        var request = fixture.Request(populationId, "csv", "annual-csv");
        var context = fixture.Context(request);

        // Act
        var result = await fixture.Handler().HandleAsync(context, CancellationToken.None);
        var population = await fixture.PopulationAsync(populationId);
        var artifact = result.IsSuccess ? await fixture.ArtifactAsync(result.Value.Export.ArtifactId) : null;
        var bytes = result.IsSuccess ? await fixture.ArtifactBytesAsync(artifact!) : [];

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var export = Assert.Single(population.Current!.Exports);
        Assert.Equal(result.Value.Export, export);
        Assert.Equal(1, export.PopulationVersion);
        Assert.Equal("csv", export.Format);
        Assert.Equal("available", result.Value.ArtifactState);
        Assert.Equal(EvidenceArtifactStates.Available, artifact!.State);
        Assert.Equal("confidential", artifact.Content!.HandlingClass);
        Assert.Equal(population.Current.Current!.ContentSha256, export.PopulationContentSha256);
        Assert.Equal(population.Current.CloseSnapshot.CloseSnapshotId, export.CloseSnapshotId);
        Assert.Equal(Hash(bytes), export.OutputContentSha256);
        Assert.Equal(bytes.LongLength, export.OutputContentLength);

        var csv = Encoding.UTF8.GetString(bytes);
        Assert.Contains("population_id,population_version,population_content_sha256,close_snapshot_id,row_id,classification,change_id,control\n", csv);
        Assert.Contains(",included,change-17,AC-1\n", csv);
        Assert.DoesNotContain("classification rationale", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("source reference", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unapproved_secret", csv, StringComparison.Ordinal);

        var retention = await fixture.RetentionAsync(export.ArtifactId);
        Assert.Equal(new DateOnly(2026, 1, 1), retention.Basis!.PeriodStart);
        Assert.Equal(new DateOnly(2026, 12, 31), retention.Basis.PeriodEnd);
        Assert.Equal(new DateOnly(2033, 12, 31), retention.Basis.RetainsThrough);
        Assert.Equal(1, fixture.Inspector.Calls);
    }

    [Fact]
    public async Task ShouldReturnTheSameExportAndArtifactGivenAnExactRetry()
    {
        // Arrange
        await using var fixture = new Fixture(ArtifactInspectionState.Clean);
        var populationId = await fixture.SeedPopulationAsync(frozen: true);
        var context = fixture.Context(fixture.Request(populationId, "csv", "stable-retry"));

        // Act
        var first = await fixture.Handler().HandleAsync(context, CancellationToken.None);
        var firstPopulation = await fixture.PopulationAsync(populationId);
        var second = await fixture.Handler().HandleAsync(context, CancellationToken.None);
        var secondPopulation = await fixture.PopulationAsync(populationId);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(first.Value.Export.ExportId, second.Value.Export.ExportId);
        Assert.Equal(first.Value.Export.ArtifactId, second.Value.Export.ArtifactId);
        Assert.Equal(first.Value.Export.GeneratedAt, second.Value.Export.GeneratedAt);
        Assert.Equal(firstPopulation.Revision, secondPopulation.Revision);
        Assert.Single(secondPopulation.Current!.Exports);
        Assert.Equal(1, fixture.Inspector.Calls);
    }

    [Fact]
    public async Task ShouldRejectChangedFormatGivenAReusedExportKey()
    {
        // Arrange
        await using var fixture = new Fixture(ArtifactInspectionState.Clean);
        var populationId = await fixture.SeedPopulationAsync(frozen: true);
        var first = await fixture.Handler().HandleAsync(fixture.Context(
            fixture.Request(populationId, "csv", "one-export-identity")), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);

        // Act
        var changedIntent = await fixture.Handler().HandleAsync(fixture.Context(
            fixture.Request(populationId, "json", "one-export-identity")), CancellationToken.None);
        var population = await fixture.PopulationAsync(populationId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changedIntent.Error?.Kind);
        Assert.Single(population.Current!.Exports);
        Assert.Equal("csv", population.Current.Exports.Single().Format);
        Assert.Equal(1, fixture.Inspector.Calls);
    }

    [Fact]
    public async Task ShouldExportTheOriginalFrozenVersionGivenAnActiveAmendment()
    {
        // Arrange
        await using var fixture = new Fixture(ArtifactInspectionState.Clean);
        var populationId = await fixture.SeedPopulationAsync(frozen: true);
        await fixture.AmendPopulationAsync(populationId);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(
            fixture.Request(populationId, "csv", "frozen-version-during-amendment")), CancellationToken.None);
        var population = await fixture.PopulationAsync(populationId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("amending", population.Current!.Status);
        Assert.Single(population.Current.Versions);
        Assert.Equal(1, result.Value.Export.PopulationVersion);
        Assert.Equal(population.Current.Versions[0].ContentSha256,
            result.Value.Export.PopulationContentSha256);
    }

    [Fact]
    public async Task ShouldKeepExportUnavailableGivenIndeterminateInspection()
    {
        // Arrange
        await using var fixture = new Fixture(ArtifactInspectionState.NotInspected);
        var populationId = await fixture.SeedPopulationAsync(frozen: true);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(
            fixture.Request(populationId, "csv", "pending-inspection")), CancellationToken.None);
        var artifact = result.IsSuccess ? await fixture.ArtifactAsync(result.Value.Export.ArtifactId) : null;

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(EvidenceArtifactStates.PendingInspection, result.Value.ArtifactState);
        Assert.Equal(EvidenceArtifactStates.PendingInspection, artifact!.State);
    }

    [Fact]
    public async Task ShouldKeepMalwareQuarantinedGivenAnExportInspectionVerdict()
    {
        // Arrange
        await using var fixture = new Fixture(ArtifactInspectionState.Quarantined);
        var populationId = await fixture.SeedPopulationAsync(frozen: true);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(
            fixture.Request(populationId, "csv", "malware-quarantine")), CancellationToken.None);
        var artifact = result.IsSuccess ? await fixture.ArtifactAsync(result.Value.Export.ArtifactId) : null;
        await using var content = artifact is not null
            ? await fixture.OpenArtifactAsync(artifact)
            : null;

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(EvidenceArtifactStates.Quarantined, result.Value.ArtifactState);
        Assert.Equal(EvidenceArtifactStates.Quarantined, artifact!.State);
        Assert.Null(content);
        Assert.NotNull((await fixture.RetentionAsync(result.Value.Export.ArtifactId)).Basis);
    }

    [Fact]
    public async Task ShouldEmitStableJsonGivenFrozenPopulationAndJsonFormat()
    {
        // Arrange
        await using var fixture = new Fixture(ArtifactInspectionState.Clean);
        var populationId = await fixture.SeedPopulationAsync(frozen: true);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(
            fixture.Request(populationId, "json", "json-export")), CancellationToken.None);
        var artifact = result.IsSuccess ? await fixture.ArtifactAsync(result.Value.Export.ArtifactId) : null;
        var bytes = result.IsSuccess ? await fixture.ArtifactBytesAsync(artifact!) : [];
        using var json = JsonDocument.Parse(bytes);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var root = json.RootElement;
        Assert.Equal(1, root.GetProperty("population_version").GetInt32());
        var row = Assert.Single(root.GetProperty("rows").EnumerateArray().ToArray());
        Assert.Equal("included", row.GetProperty("classification").GetString());
        var fields = row.GetProperty("fields");
        Assert.Equal("change-17", fields.GetProperty("change_id").GetString());
        Assert.Equal("AC-1", fields.GetProperty("control").GetString());
        Assert.False(row.TryGetProperty("classification_rationale", out _));
        Assert.DoesNotContain("unapproved_secret", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldNeutralizeSpreadsheetFormulasGivenCsvFieldValues()
    {
        // Arrange
        await using var fixture = new Fixture(ArtifactInspectionState.Clean);
        var populationId = await fixture.SeedPopulationAsync(frozen: true, "=HYPERLINK(1)");

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(
            fixture.Request(populationId, "csv", "formula-safe-export")), CancellationToken.None);
        var artifact = result.IsSuccess ? await fixture.ArtifactAsync(result.Value.Export.ArtifactId) : null;
        var bytes = result.IsSuccess ? await fixture.ArtifactBytesAsync(artifact!) : [];

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(",'=HYPERLINK(1),AC-1\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenPopulationExport()
    {
        // Arrange
        await using var fixture = new Fixture(ArtifactInspectionState.Clean);
        var populationId = await fixture.SeedPopulationAsync(frozen: true);
        var context = fixture.Context(fixture.Request(populationId, "csv", "system-actor"), RequestActor.System);

        // Act
        var result = await fixture.Handler().HandleAsync(context, CancellationToken.None);
        var population = await fixture.PopulationAsync(populationId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(population.Current!.Exports);
        Assert.Equal(0, fixture.Inspector.Calls);
    }

    [Fact]
    public async Task ShouldRejectExportGivenUnfrozenPopulation()
    {
        // Arrange
        await using var fixture = new Fixture(ArtifactInspectionState.Clean);
        var populationId = await fixture.SeedPopulationAsync(frozen: false);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(
            fixture.Request(populationId, "csv", "too-early")), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, fixture.Inspector.Calls);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly string _root = Path.Combine(Path.GetTempPath(), $"bdgrz-population-export-{Guid.NewGuid():N}");
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly TimeProvider _clock = new FixedTimeProvider(Now.AddHours(1));
        readonly LocalArtifactContentStore _store;
        readonly Inspector _inspector;
        readonly Bdgrz.Compliance.Features.Content.IImmutableContentStore _content =
            new Bdgrz.Compliance.Features.Content.FitzKvImmutableContentStore(new Cntryl.Fitz.Testing.InMemoryKvClient());

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        public Inspector Inspector => _inspector;
        public InMemoryEventStore Events { get; } = new();
        public AuditPopulationReader Populations => new(Reader, Events, _content, new EventStoreSourceFactResolver(Events));
        public AuditPopulationMutations Mutations => new(new NativeOperationExecutor(Reader, Executor, _content), Reader,
            Executor, Events, _content, new EventStoreSourceFactResolver(Events), new TypeIIPeriodReader(Reader, Events, _content),
            new AuditPopulationSources(Events, new EventStoreSourceFactResolver(Events), _content), _clock);

        public Fixture(ArtifactInspectionState inspectionState)
        {
            _store = new LocalArtifactContentStore(new ArtifactContentStoreOptions(_root, new byte[32], 8 * 1024 * 1024), _clock);
            _inspector = new Inspector(inspectionState, _store);
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(Events);
            services.AddSingleton<IDomainEventReader>(Events);
            services.AddSingleton<IImmutableContentStore>(_content);
            services.AddSingleton<ISourceFactResolver>(provider => new EventStoreSourceFactResolver(provider.GetRequiredService<IDomainEventReader>()));
            services.AddPortia();
            services.AddVerifiedAuditPopulationReader();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public GenerateAuditPopulationExportHandler Handler() => new(Mutations, Populations, Reader,
            new EvidenceIntake(_store, _inspector, Reader, Writer, _clock), _clock,
            new Bdgrz.Compliance.Features.NativeOperations.NativeOperationExecutor(Reader, Executor, _content), _content, _scope.ServiceProvider.GetRequiredService<IDomainEventReader>());

        public GenerateAuditPopulationExport Request(Uuid populationId, string format, string key) =>
            new(TenantId, ProgramId, populationId, 1, format, key);

        public IRequestContext<GenerateAuditPopulationExport> Context(GenerateAuditPopulationExport request,
            ClaimsPrincipal? actor = null) => new ExportContext(request, actor ?? Actor(UserId));

        public async Task<Uuid> SeedPopulationAsync(bool frozen, string changeId = "change-17")
        {
            var populationId = Uuid.CreateVersion4();
            var actorId = RbacIds.Member(TenantId, UserId);
            var population = new AuditPopulationAggregate(TenantId, ProgramId, populationId);
            Assert.Null(population.Define(populationId, Definition(), ClosedPeriod(TenantId, ProgramId), actorId,
                "Compliance lead", Now));
            Assert.Null(population.RecordFacts(population.Revision, SourceSnapshot(), [Row(changeId)], actorId,
                "Compliance lead", Now.AddMinutes(1)));
            Assert.Null(population.Reconcile(population.Revision,
                new AuditPopulationReconciliationContent(1, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                    "Every source row is accounted for."), actorId, "Compliance lead", Now.AddMinutes(2)));
            if (frozen)
                Assert.Null(population.Freeze(population.Revision,
                    "The source rows and reconciliation were reviewed.", actorId, "Compliance lead", Now.AddMinutes(3)));
            await AuditPopulationSeeds.AppendLegacyAsync(Events, population);
            return populationId;
        }

        public async Task AmendPopulationAsync(Uuid populationId)
        {
            var complete = await Populations.ReadAsync(TenantId, ProgramId, populationId);
            var population = complete.Rules;
            Assert.NotNull(population.Current);
            Assert.Null(population.Amend(population.Revision, "Record a late source row.",
                new AuditPopulationSourceSnapshotContent(Uuid.CreateVersion4(), 2,
                    "approved-change-register-amended", Hash('e'), 2, true,
                    "The amendment preserves the original frozen population and adds one late row.",
                    Now.AddMinutes(4)), [Row(Uuid.CreateVersion4())], RbacIds.Member(TenantId, UserId),
                "Compliance lead", Now.AddMinutes(4)));
            await AuditPopulationSeeds.AppendLegacyAsync(Events, population, complete.Owner.CommittedStreamPosition);
        }

        public async Task<AuditPopulationAggregate> PopulationAsync(Uuid id) =>
            (await Populations.ReadAsync(TenantId, ProgramId, id)).Rules;

        public Task<EvidenceArtifact> ArtifactAsync(Uuid id) =>
            Reader.HydrateAsync(new EvidenceArtifact(TenantId, id)).AsTask();

        public async Task<ArtifactRetentionBasis> RetentionAsync(Uuid artifactId)
        {
            var artifact = await Reader.HydrateAsync(new EvidenceArtifact(TenantId, artifactId));
            return await Reader.HydrateAsync(new ArtifactRetentionBasis(new(TenantId, "evidence_artifact",
                artifactId, artifact.ContentSha256!)));
        }

        public async Task<byte[]> ArtifactBytesAsync(EvidenceArtifact artifact)
        {
            await using var content = await _store.OpenVerifiedAsync(new ArtifactContentReference(TenantId,
                artifact.ContentSha256!, artifact.ContentLength!.Value));
            Assert.NotNull(content);
            using var output = new MemoryStream();
            await content.CopyToAsync(output);
            return output.ToArray();
        }

        public ValueTask<Stream?> OpenArtifactAsync(EvidenceArtifact artifact) =>
            _store.OpenVerifiedAsync(new ArtifactContentReference(TenantId,
                artifact.ContentSha256!, artifact.ContentLength!.Value));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        static AuditPopulationDefinitionContent Definition() => new("Production changes",
            "Production changes affecting the in-scope service.", "Approved change register",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "Include approved changes.",
            "Exclude canceled changes.", "Reconcile every row to the source register.",
            ["control", "change_id"], [Uuid.CreateVersion4()]);

        static AuditPopulationSourceSnapshotContent SourceSnapshot() => new(Uuid.CreateVersion4(), 1,
            "approved-change-register", Hash('d'), 1, true, "All source rows are included.", Now.AddMinutes(1));

        static AuditPopulationRowContent Row(string changeId) => new(Uuid.CreateVersion4(), changeId,
            "Approved change register", "included", "unapproved_secret classification rationale",
            new Dictionary<string, string> { ["control"] = "AC-1", ["change_id"] = changeId }, [], []);

        static AuditPopulationRowContent Row(Uuid rowId) => new(rowId, "change-18",
            "Approved change register", "included", "The late row belongs in the amended population.",
            new Dictionary<string, string> { ["control"] = "AC-1", ["change_id"] = "change-18" }, [], []);

        static TypeIIPeriodView ClosedPeriod(Uuid tenantId, Uuid programId) => new(tenantId, programId,
            Uuid.CreateVersion4(), "closed", 4, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, Hash('b'),
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Uuid.CreateVersion4(), Hash('c'), [], [], [], [],
            null, [], [new TypeIIPeriodCloseSnapshotView(Uuid.CreateVersion4(), 1, [], [], [], Hash('a'), null, null,
                [], [], Uuid.CreateVersion4(), "ceo", "CEO", Now)], Uuid.CreateVersion4(), "Compliance lead", Now);

        static string Hash(char value) => new(value, 64);

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "lead@example.test")], "BdgrzSession"));

        sealed class ExportContext(GenerateAuditPopulationExport request, ClaimsPrincipal actor)
            : IRequestContext<GenerateAuditPopulationExport>
        {
            public GenerateAuditPopulationExport Request => request;
            public ClaimsPrincipal Actor => actor;
            public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
            public Uuid RequestId { get; } = Uuid.CreateVersion4();
            public Uuid CorrelationId => RequestId;
            public Uuid? CausationId => null;
            public Uuid CauseId => RequestId;
            public DateTimeOffset StartedAt { get; } = Now.AddHours(1);
            public RequestInvocation Invocation { get; } = new HttpInvocation("POST",
                "/programs/audit-populations/export", "/programs/audit-populations/export", "test");
        }

        sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => utcNow;
        }
    }

    sealed class Inspector(ArtifactInspectionState state, IArtifactContentStore store) : IArtifactInspector
    {
        public int Calls { get; private set; }

        public async ValueTask<ArtifactInspectionResult> InspectAsync(ArtifactContentReference content,
            CancellationToken ct = default)
        {
            Calls++;
            await using var verified = await store.OpenVerifiedAsync(content, ct);
            Assert.NotNull(verified);
            return new ArtifactInspectionResult(state);
        }
    }

    static string Hash(byte[] content) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content))
        .ToLowerInvariant();
}
