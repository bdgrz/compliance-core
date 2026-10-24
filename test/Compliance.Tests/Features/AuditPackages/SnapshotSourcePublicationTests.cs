using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.Operations;
using System.IO.Compression;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AuditPackages;

public sealed class SnapshotSourcePublicationTests
{
    [Fact]
    public async Task ShouldPublishExactSourceBytesGivenProductionContributorAndBaselineFreeze()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var preview = await fixture.Previews.ReadAsync(fixture.Engagement!, fixture.UserId);
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        var request = new FreezeTypeIBaseline(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId,
            fixture.BaselineId, fixture.Engagement!.Revision, "Confirmed actual source bodies", preview.Value.ContentSha256);

        // Act
        var frozen = await new FreezeTypeIBaselineHandler(fixture.Reader, fixture.Executor, fixture.Directory,
            fixture.Previews, fixture.Clock).HandleAsync(fixture.Context(request), CancellationToken.None);
        var expected = fixture.BoundaryBytes();
        var sha = Convert.ToHexStringLower(SHA256.HashData(expected));
        Assert.Equal(preview.Value.Sources.Single(source => source.Kind == "approved_boundary").ContentSha256, sha);
        Assert.Equal(preview.Value.Sources.Single(source => source.Kind == "approved_boundary").ContentSha256, sha);
        await using var bytes = await fixture.Artifacts.OpenVerifiedAsync(
            new ArtifactContentReference(fixture.TenantId, sha, expected.Length));

        // Assert
        Assert.True(frozen.IsSuccess, frozen.Error?.Message);
        Assert.NotNull(bytes);
        using var received = new MemoryStream();
        await bytes.CopyToAsync(received);
        Assert.Equal(expected, received.ToArray());
        var saved = await fixture.Reader.HydrateAsync(new ExaminationEngagement(fixture.TenantId,
            fixture.ProgramId, fixture.ExaminationId));
        Assert.NotNull(saved.Baseline!.SourceContentManifest);
        Assert.All(saved.Baseline.Sources, source => Assert.Null(source.Content));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReturnClassifiedFailureWithoutFreezingGivenUnreadablePublicationManifest(bool unavailable)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var reader = new PublishedTypeIBaselinePreviewReader(fixture.Sources, fixture.Artifacts,
            new UnreadablePublicationContent(fixture.Content, unavailable));
        var before = await fixture.Reader.HydrateAsync(new ExaminationEngagement(fixture.TenantId,
            fixture.ProgramId, fixture.ExaminationId));

        // Act
        var result = await reader.ReadAsync(fixture.Engagement!, fixture.UserId);
        var after = await fixture.Reader.HydrateAsync(new ExaminationEngagement(fixture.TenantId,
            fixture.ProgramId, fixture.ExaminationId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.Null(after.Baseline);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldKeepPreviewIdentityStableGivenRepeatedPhysicalPublication()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();

        // Act
        var first = await fixture.Previews.ReadAsync(fixture.Engagement!, fixture.UserId);
        var second = await fixture.Previews.ReadAsync(fixture.Engagement!, fixture.UserId);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.NotEqual(first.Value.SourceContentManifest!.ContentId, second.Value.SourceContentManifest!.ContentId);
        Assert.Equal(first.Value.SourceContentManifest.Sha256, second.Value.SourceContentManifest.Sha256);
        Assert.Equal(first.Value.ContentSha256, second.Value.ContentSha256);
        var boundary = first.Value.Sources.Single(source => source.Kind == "approved_boundary");
        Assert.Equal("snapshot", boundary.Content!.FileExtension);
        Assert.Equal("application/vnd.bdgrz.canonical-snapshot-source.v1", boundary.Content.MediaType);
    }

    [Fact]
    public async Task ShouldArchiveFullWorkforceRowsGivenFrozenLogicalMerkleSource()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var baseline = await fixture.FreezeAsync();
        var directory = fixture.Directory;
        directory.Baseline = baseline;
        var baselineRead = await new GetTypeIBaselineHandler(directory, fixture.Content).HandleAsync(
            fixture.Context(new GetTypeIBaseline(fixture.TenantId, fixture.ProgramId, fixture.BaselineId)),
            CancellationToken.None);
        var source = baselineRead.Value!.Sources.Single(row => row.Kind == "workforce_roster_snapshot");
        Assert.NotNull(source.Content);
        Assert.NotEqual(source.ContentSha256, source.Content.Sha256);
        var packageId = Uuid.CreateVersion4();
        var create = new CreateAuditPackage(fixture.TenantId, fixture.ProgramId, packageId,
            new AuditPackageContent(fixture.ExaminationId, "type_i", "type_i_baseline", fixture.BaselineId,
                baseline.ContentSha256, "Actual workforce package", [new AuditPackageSelectionEntry("workforce",
                    "roster", source.Kind, source.RecordId, source.Revision, source.Content.FileExtension,
                    source.ContentSha256, source.Content.Bytes, "product_calculated", source.VersionId)]));
        var packages = new AuditPackageReader(fixture.Reader, fixture.Content);
        var root = new AuditPackageRootOperation(fixture.Reader, packages,
            new NativeOperationExecutor(fixture.Reader, fixture.Executor, fixture.Content), fixture.Content, fixture.Clock);
        var created = await new CreateAuditPackageHandler(root, new AuditPackageSourceBuilder(fixture.Reader, fixture.Periods,
            fixture.Content), fixture.Content).HandleAsync(fixture.Context(create), CancellationToken.None);
        Assert.True(created.IsSuccess, created.Error?.Message);
        var requested = await new RequestAuditPackageBuildHandler(root, fixture.Content).HandleAsync(fixture.Context(
            new RequestAuditPackageBuild(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId,
                packageId, "type_i", "canonical-rows", 1, 1)), CancellationToken.None);
        Assert.True(requested.IsSuccess, requested.Error?.Message);
        var owner = await fixture.Reader.HydrateAsync(new AuditPackageManifest(fixture.TenantId, packageId));
        var admitted = Assert.Single(owner.Facts.OfType<AuditPackageBuildRequested>());
        var proof = NativeFactReference.Of(owner.Stream, admitted);
        var outputs = new FitzKvAuditPackageBuildOutputs(fixture.Kv);
        var start = new StartAuditPackageBuild(EffectIdentity.CommandId(proof.EventId,
            $"{AuditPackageBuildProcess.Name}:{nameof(StartAuditPackageBuild)}", owner.Stream.ToString(), "1"), proof,
            owner.Stream, admitted.Operation.RootOperationId, 1, null, admitted.Change, packageId,
            admitted.Operation.LogicalId, 1);

        // Act
        var built = await new StartAuditPackageBuildHandler(new AuditPackageSourceVerifier(fixture.Reader,
            fixture.Content), new AuditPackageArchiveBuilder(fixture.Artifacts), outputs, fixture.Content).HandleAsync(
            System(start), CancellationToken.None);
        Assert.True(built.IsSuccess, built.Error?.Message);
        var outputProof = await outputs.ReadAsync(fixture.TenantId, start.BuildId, CancellationToken.None);
        var output = await fixture.Content.ReadJsonAsync<AuditPackageBuildOutput>(fixture.TenantId.ToString(),
            outputProof!.VerifiedOutput);
        await using var stored = await fixture.Artifacts.OpenVerifiedAsync(new ArtifactContentReference(fixture.TenantId,
            output.ArchiveSha256!, output.ArchiveBytes!.Value));
        using var zip = new ZipArchive(stored!, ZipArchiveMode.Read);
        var entry = Assert.Single(zip.Entries);
        await using var payload = entry.Open();
        using var json = await JsonDocument.ParseAsync(payload);

        // Assert
        var rows = json.RootElement.GetProperty("rows");
        Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal("Alice", rows[0].GetProperty("content").GetProperty("name").GetString());
        Assert.Equal("Bob", rows[1].GetProperty("content").GetProperty("name").GetString());
        var recovered = rows.EnumerateArray().Select(row => new PopulationRow(row.GetProperty("key").GetString()!,
            row.GetProperty("content").Clone())).ToArray();
        Assert.Equal(source.ContentSha256, PopulationContentIdentity.Compute("workforce_roster", recovered).Value.Sha256);
        Assert.Equal(source.ContentSha256, json.RootElement.GetProperty("logical_content_sha256").GetString());
        Assert.Equal(source.Content.Bytes, entry.Length);
        Assert.EndsWith(".json", entry.FullName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRejectPhysicalOverrideGivenFrozenLogicalSourceProof()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var baseline = await fixture.FreezeAsync();
        fixture.Directory.Baseline = baseline;
        var read = await new GetTypeIBaselineHandler(fixture.Directory, fixture.Content).HandleAsync(
            fixture.Context(new GetTypeIBaseline(fixture.TenantId, fixture.ProgramId, fixture.BaselineId)),
            CancellationToken.None);
        var source = read.Value!.Sources.Single(row => row.Kind == "workforce_roster_snapshot");
        var selected = new AuditPackageSelectionEntry("workforce", "roster", source.Kind, source.RecordId,
            source.Revision, source.Content!.FileExtension, source.ContentSha256, source.Content.Bytes,
            "product_calculated", source.VersionId, source.Content with { Sha256 = new string('f', 64) });
        var content = new AuditPackageContent(fixture.ExaminationId, "type_i", "type_i_baseline", fixture.BaselineId,
            baseline.ContentSha256, "Exact sources", [selected]);

        // Act
        var result = await new AuditPackageSourceBuilder(fixture.Reader, fixture.Periods, fixture.Content).ResolveAsync(fixture.TenantId,
            fixture.ProgramId, content, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    static Cntryl.Portia.RequestContext<T> System<T>(T request) where T : IRequestBase =>
        new(request, new RequestDispatchContext(RequestActor.System));

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _kernel;
        readonly AsyncServiceScope _scope;
        ServiceProvider? _production;
        AsyncServiceScope _productionScope;
        readonly string _root = Path.Combine(Path.GetTempPath(), "bdgrz-publication-test-" + Guid.NewGuid().ToString("N"));
        readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineId { get; } = Uuid.CreateVersion4();
        public Uuid BoundaryId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public PersistenceKvClient Kv { get; } = new(new InMemoryKvClient());
        public IImmutableContentStore Content { get; }
        public LocalArtifactContentStore Artifacts { get; }
        public InMemoryEventStore PeriodEvents { get; } = new();
        public TypeIIPeriodReader Periods => new(Reader, PeriodEvents, Content);
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        public ITypeIBaselinePreviewReader Previews { get; private set; } = null!;
        public TypeIBaselinePreviewReader Sources => _productionScope.ServiceProvider.GetRequiredService<TypeIBaselinePreviewReader>();
        public ExaminationEngagementView? Engagement { get; private set; }
        public BoundaryVersionView Boundary { get; private set; } = null!;
        public EngagementDirectory Directory { get; } = new();
        public TimeProvider Clock => new FixedClock(_now.AddMinutes(1));

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(PeriodEvents);
            services.AddPortia();
            _kernel = services.BuildServiceProvider();
            _scope = _kernel.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            Content = new FitzKvImmutableContentStore(Kv);
            Artifacts = new LocalArtifactContentStore(new ArtifactContentStoreOptions(_root, null,
                ArtifactContentLimits.MaxContentLength), Clock);
        }

        public async Task SeedAsync()
        {
            var member = RbacIds.Member(TenantId, UserId);
            var exam = new ExaminationEngagement(TenantId, ProgramId, ExaminationId);
            Assert.Null(exam.Create(new ExaminationEngagementContent("type_i", "Audit Firm",
                DateOnly.FromDateTime(_now.UtcDateTime), null, ["security"], BoundaryId, [member]), member,
                "Examiner", _now));
            await Writer.SaveAsync(exam, new RequestDispatchContext(RequestActor.System));
            Engagement = exam.Current;
            Directory.Engagement = Engagement;
            Boundary = new BoundaryVersionView(TenantId, BoundaryId, ProgramId, Uuid.CreateVersion4(), 2,
                new BoundaryContent("Scope", "type_i", ["security"], []), "approved", Engagement!.PeriodStart,
                member, "Approver", _now);
            var boundaryView = new BoundaryView(TenantId, BoundaryId, ProgramId, null, Boundary, null, 3);
            var boundaries = new BoundaryReader(boundaryView, Boundary);
            var emptyEvents = new InMemoryEventStore();
            var boundaryContributor = new BoundaryTypeIBaselineSourceContributor(boundaries,
                new ProgramSetupWorkReadConsistency(new ProgramReader(), boundaries, emptyEvents));
            PopulationRow[] rows = [new("person-1", JsonSerializer.SerializeToElement(new { name = "Alice" })),
                new("person-2", JsonSerializer.SerializeToElement(new { name = "Bob" }))];
            var digest = PopulationContentIdentity.Compute("workforce_roster", rows).Value;
            var snapshotId = Uuid.CreateVersion4();
            var snapshot = new PopulationSnapshot(TenantId, snapshotId);
            var author = ActorReference.ForMember(member, "Workforce owner");
            Assert.True(snapshot.Freeze(snapshotId, null, "workforce_roster", rows, digest.Sha256, null, author, _now).IsSuccess);
            await Writer.SaveAsync(snapshot, new RequestDispatchContext(RequestActor.System));
            var directory = new PopulationDirectory(new PopulationSnapshotSummary(TenantId, snapshotId, snapshotId,
                null, "workforce_roster", rows.Length, digest.Sha256, null, author, _now));
            var workforce = new WorkforceTypeIBaselineSourceContributor(directory, Reader,
                new PopulationSnapshotDirectoryReadConsistency(directory, emptyEvents));
            var contributors = TypeIBaselineSourceFamilies.Required.Select(family => family switch
            {
                "approved_boundary" => (ITypeIBaselineSourceContributor)boundaryContributor,
                "workforce" => workforce,
                _ => new EmptyContributor(family),
            }).ToArray();
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Fitz:Endpoint"] = "ws://fitz.invalid/ws", ["Fitz:ApplicationName"] = "publication-test" }).Build(),
                developerAuthentication: true);
            services.AddSingleton<IEnumerable<ITypeIBaselineSourceContributor>>(contributors);
            services.AddSingleton(Content);
            services.AddSingleton<IArtifactContentStore>(Artifacts);
            _production = services.BuildServiceProvider();
            _productionScope = _production.CreateAsyncScope();
            Previews = _productionScope.ServiceProvider.GetRequiredService<ITypeIBaselinePreviewReader>();
        }

        public async Task<TypeIBaselineView> FreezeAsync()
        {
            var preview = await Previews.ReadAsync(Engagement!, UserId);
            Assert.True(preview.IsSuccess, preview.Error?.Message);
            var result = await new FreezeTypeIBaselineHandler(Reader, Executor, Directory, Previews, Clock).HandleAsync(
                Context(new FreezeTypeIBaseline(TenantId, ProgramId, ExaminationId, BaselineId,
                    Engagement!.Revision, "Freeze complete source bytes", preview.Value.ContentSha256)), CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error?.Message);
            return (await Reader.HydrateAsync(new ExaminationEngagement(TenantId, ProgramId, ExaminationId))).Baseline!;
        }

        public byte[] BoundaryBytes()
        {
            var element = JsonSerializer.SerializeToElement(Boundary, ComplianceCoreJsonContext.Default.BoundaryVersionView);
            using var bytes = new MemoryStream();
            bytes.Write(Encoding.UTF8.GetBytes("bdgrz.snapshot.source.approved_boundary_version.v1\0"));
            using (var writer = new Utf8JsonWriter(bytes, SnapshotContentIdentity.CanonicalWriterOptions))
                SnapshotContentIdentity.WriteCanonical(writer, element);
            return bytes.ToArray();
        }

        public Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request,
            new RequestDispatchContext(new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"),
                new Claim("sub", UserId.ToString()), new Claim("email", "publisher@example.test")], "BdgrzSession")),
                new HttpInvocation("PUT", "/api/v1/source-test", "/api/v1/source-test", "source-test")));
        public async ValueTask DisposeAsync()
        {
            if (_production is not null)
            {
                await _productionScope.DisposeAsync();
                await _production.DisposeAsync();
            }
            await _scope.DisposeAsync();
            await _kernel.DisposeAsync();
            if (global::System.IO.Directory.Exists(_root))
                global::System.IO.Directory.Delete(_root, recursive: true);
        }
    }

    sealed class UnreadablePublicationContent(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            throw (unavailable ? new ImmutableContentUnavailableException("The publication manifest is temporarily unavailable.")
                : new ImmutableContentIntegrityException("The publication manifest cannot be verified."));

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    sealed class EmptyContributor(string family) : ITypeIBaselineSourceContributor
    {
        public string Family => family;
        public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(ExaminationEngagementView engagement,
            CancellationToken ct) => ValueTask.FromResult(Result<TypeIBaselineSourceContribution>.Success(
                new TypeIBaselineSourceContribution(family, [], [], [])));
    }

    sealed class EngagementDirectory : IExaminationEngagementDirectoryReader
    {
        public ExaminationEngagementView? Engagement { get; set; }
        public TypeIBaselineView? Baseline { get; set; }
        public ValueTask<ExaminationEngagementView?> GetAsync(Uuid tenantId, Uuid examinationEngagementId,
            CancellationToken ct = default) => ValueTask.FromResult(Engagement);
        public ValueTask<TypeIBaselineView?> GetBaselineAsync(Uuid tenantId, Uuid typeIBaselineId,
            CancellationToken ct = default) => ValueTask.FromResult(Baseline);
    }

    sealed class PopulationDirectory(PopulationSnapshotSummary summary) : IPopulationSnapshotDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(ProjectionCheckpoint.Start);
        public ValueTask<Page<PopulationSnapshotSummary>> ListAsync(Uuid tenantId, string kind, int limit,
            string? cursor, CancellationToken ct = default) => ValueTask.FromResult(new Page<PopulationSnapshotSummary>([summary], null));
    }

    sealed class ProgramReader : Bdgrz.Compliance.Features.Programs.IProgramDirectoryReader
    {
        public ValueTask<Bdgrz.Compliance.Features.Programs.ProgramView?> GetAsync(Uuid tenantId,
            Uuid programId, CancellationToken ct = default) => ValueTask.FromResult<
            Bdgrz.Compliance.Features.Programs.ProgramView?>(null);

        public ValueTask<Page<Bdgrz.Compliance.Features.Programs.ProgramView>> ListAsync(
            Uuid tenantId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<Bdgrz.Compliance.Features.Programs.ProgramView>([], null));

        public ValueTask<Page<Bdgrz.Compliance.Features.Programs.ProgramRevisionView>?>
            ListRevisionsAsync(Uuid tenantId, Uuid programId, int limit, string? cursor,
                CancellationToken ct = default) => ValueTask.FromResult<
                Page<Bdgrz.Compliance.Features.Programs.ProgramRevisionView>?>(null);

        public ValueTask<Bdgrz.Compliance.Features.Programs.ProgramRevisionView?> GetRevisionAsync(
            Uuid tenantId, Uuid programId, long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<Bdgrz.Compliance.Features.Programs.ProgramRevisionView?>(null);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class BoundaryReader(Bdgrz.Compliance.Features.Boundaries.BoundaryView boundary,
            Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView effectiveVersion)
            : Bdgrz.Compliance.Features.Boundaries.IBoundaryDirectoryReader
    {
        public ValueTask<Bdgrz.Compliance.Features.Boundaries.BoundaryView?> GetAsync(
            Uuid tenantId, Uuid boundaryId, CancellationToken ct = default) =>
            ValueTask.FromResult<Bdgrz.Compliance.Features.Boundaries.BoundaryView?>(
                tenantId == boundary.TenantId && boundaryId == boundary.BoundaryId ? boundary : null);

        public ValueTask<Page<Bdgrz.Compliance.Features.Boundaries.BoundaryView>> ListProgramAsync(
            Uuid tenantId, Uuid programId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(
            new Page<Bdgrz.Compliance.Features.Boundaries.BoundaryView>([], null));

        public ValueTask<Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView?> GetVersionAsync(
            Uuid tenantId, Uuid boundaryId, Uuid versionId, CancellationToken ct = default) =>
            ValueTask.FromResult<Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView?>(
                tenantId == effectiveVersion.TenantId && boundaryId == effectiveVersion.BoundaryId &&
                versionId == effectiveVersion.VersionId ? effectiveVersion : null);

        public ValueTask<Page<Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView>?>
            ListVersionsAsync(Uuid tenantId, Uuid boundaryId, int limit, string? cursor,
                CancellationToken ct = default) => ValueTask.FromResult<
                Page<Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView>?>(null);

        public ValueTask<Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView?>
            GetEffectiveVersionAsync(Uuid tenantId, Uuid boundaryId, DateOnly effectiveOn,
                CancellationToken ct = default) => ValueTask.FromResult<
                Bdgrz.Compliance.Features.Boundaries.BoundaryVersionView?>(
                tenantId == effectiveVersion.TenantId && boundaryId == effectiveVersion.BoundaryId &&
                effectiveOn >= effectiveVersion.EffectiveFrom ? effectiveVersion : null);

        public ValueTask<Bdgrz.Compliance.Features.Boundaries.BoundaryDecisionView?> GetDecisionAsync(
            Uuid tenantId, Uuid boundaryId, Uuid decisionId, CancellationToken ct = default) =>
            ValueTask.FromResult<Bdgrz.Compliance.Features.Boundaries.BoundaryDecisionView?>(null);

        public ValueTask<Page<Bdgrz.Compliance.Features.Boundaries.BoundaryDecisionView>?>
            ListDecisionsAsync(Uuid tenantId, Uuid boundaryId, int limit, string? cursor,
                CancellationToken ct = default) => ValueTask.FromResult<
                Page<Bdgrz.Compliance.Features.Boundaries.BoundaryDecisionView>?>(null);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }
}
