using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Tests.Features.Examinations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using AuditPackageAggregate = Bdgrz.Compliance.Features.AuditPackages.AuditPackageRules;

namespace Bdgrz.Compliance.Tests.Features.AuditPackages;

public sealed class AmendAuditPackageHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldAmendByRemovingAnExactSelectedRecordGivenBuiltTypeIPackage()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedBaselineAsync();
        var packageId = Uuid.CreateVersion4();
        await fixture.SeedBuiltPackageAsync(packageId, baseline);
        var request = new AmendAuditPackage(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, packageId, 3, "Remove the superseded source record.", [fixture.SourceRecord1]);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.UserId,
            request), CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(4, saved.Revision);
        Assert.Equal(2, saved.Current!.CurrentVersion);
        var version = saved.Current.Versions[1];
        Assert.Equal("draft", version.Status);
        Assert.Equal(1, version.AmendsVersion);
        Assert.Equal("Remove the superseded source record.", version.AmendmentReason);
        Assert.Equal(fixture.BaselineId, version.SourceSnapshotId);
        Assert.Equal(fixture.SourceRecord1, Assert.Single(version.SelectedContent).SourceRecordId);
        Assert.Empty(version.BuildAttempts);
    }

    [Fact]
    public async Task ShouldRejectRecordOutsidePriorSelectionGivenPackageAmendment()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedBaselineAsync();
        var packageId = Uuid.CreateVersion4();
        await fixture.SeedBuiltPackageAsync(packageId, baseline);
        var request = new AmendAuditPackage(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, packageId, 3, "Attempt to add an unselected source.", [Uuid.CreateVersion4()]);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.UserId,
            request), CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(3, saved.Revision);
        Assert.Equal(1, saved.Current!.CurrentVersion);
    }

    [Fact]
    public async Task ShouldAddExactFrozenSourceGivenCompleteAmendmentSelection()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedBaselineAsync();
        var packageId = Uuid.CreateVersion4();
        var completeContent = fixture.Content(baseline);
        await fixture.SeedBuiltPackageAsync(packageId, baseline,
            selectedContent: [completeContent.SelectedContent[0]]);
        var request = new AmendAuditPackage(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, packageId, 3, "Include the additional exact baseline source.", null, completeContent);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.UserId,
            request), CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(4, saved.Revision);
        Assert.Equal(2, saved.Current!.CurrentVersion);
        Assert.Equal(completeContent.SelectedContent.Select(item => item.SourceRecordId),
            saved.Current.Versions[1].SelectedContent.Select(item => item.SourceRecordId));
        Assert.Equal(baseline.TypeIBaselineId, saved.Current.Versions[1].SourceSnapshotId);
    }

    [Fact]
    public async Task ShouldRejectChangedPackageIdentityGivenCompleteAmendmentSelection()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedBaselineAsync();
        var packageId = Uuid.CreateVersion4();
        await fixture.SeedBuiltPackageAsync(packageId, baseline);
        var content = fixture.Content(baseline) with { Name = "Attacker-selected package name" };
        var request = new AmendAuditPackage(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, packageId, 3, "Try to change package identity.", null, content);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.UserId,
            request), CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(3, saved.Revision);
        Assert.Equal(1, saved.Current!.CurrentVersion);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenPackageAmendment()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = new AmendAuditPackage(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4(), 3, "Unauthorized change.", []);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.SystemContext(request),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        static readonly Uuid ArchiveId = Uuid.CreateVersion4();
        static readonly byte[] SourceBytes1 = Enumerable.Repeat((byte)'b', 128).ToArray();
        static readonly byte[] SourceBytes2 = Enumerable.Repeat((byte)'c', 128).ToArray();
        static readonly string SourceHash1 = Convert.ToHexStringLower(SHA256.HashData(SourceBytes1));
        static readonly string SourceHash2 = Convert.ToHexStringLower(SHA256.HashData(SourceBytes2));
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly TimeProvider _clock = new FixedTimeProvider(Now);
        readonly string _artifactRoot = Path.Combine(Path.GetTempPath(),
            $"bdgrz-audit-package-tests-{Guid.NewGuid():N}");
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineId { get; } = Uuid.CreateVersion4();
        public Uuid SourceRecord1 { get; } = Uuid.CreateVersion4();
        public Uuid SourceRecord2 { get; } = Uuid.CreateVersion4();
        public Uuid SourceVersion1 { get; } = Uuid.CreateVersion4();
        public Uuid SourceVersion2 { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public InMemoryEventStore PeriodEvents { get; } = new();
        public TypeIIPeriodReader Periods => new(Reader, PeriodEvents, ContentStore);
        public ExaminationEngagementReader Examinations => new(Reader, PeriodEvents, ContentStore);
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        public IImmutableContentStore ContentStore { get; } =
            new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        public AuditPackageReader Packages => new(Reader, ContentStore, PeriodEvents);
        public AuditPackageRootOperation RootOperation => new(Reader, Packages,
            new NativeOperationExecutor(Reader, Executor, ContentStore), ContentStore, _clock);
        public async Task<AuditPackageRules> ReadPackageAsync(Uuid id) =>
            new(TenantId, ProgramId, id, (await Packages.ReadAsync(TenantId, ProgramId, id, CancellationToken.None)).View);
        public async Task<long> ManifestRevisionAsync(Uuid id) =>
            (await Packages.ReadAsync(TenantId, ProgramId, id, CancellationToken.None)).Owner.Revision;

        public IArtifactContentStore ArtifactStore { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(PeriodEvents);
            ArtifactStore = new LocalArtifactContentStore(new ArtifactContentStoreOptions(
                _artifactRoot, null, ArtifactContentLimits.MaxContentLength), _clock);
            services.AddSingleton(ArtifactStore);
            services.AddPortia();
            ExaminationSeeds.Configure(services, PeriodEvents);
            PackageSeeds.Configure(services, PeriodEvents);
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public AmendAuditPackageHandler Handler() => new(RootOperation, new AuditPackageSourceBuilder(Examinations, Periods), ContentStore);

        public async Task SeedSourceBytesAsync()
        {
            await ArtifactStore.StoreIfAbsentAsync(TenantId, new MemoryStream(SourceBytes1));
            await ArtifactStore.StoreIfAbsentAsync(TenantId, new MemoryStream(SourceBytes2));
        }

        public async Task<TypeIBaselineView> SeedBaselineAsync()
        {
            var memberId = RbacIds.Member(TenantId, UserId);
            var engagement = new ExaminationEngagementRules(TenantId, ProgramId, ExaminationId);
            var boundaryId = Uuid.CreateVersion4();
            Assert.Null(engagement.Create(new ExaminationEngagementContent("type_i",
                "Independent Audit LLP", new DateOnly(2026, 9, 30), null, ["security"],
                boundaryId, [memberId]), memberId, "Engagement lead", Now));
            var preview = TypeIBaselinePreviewTestFactory.Create(TenantId, ProgramId, ExaminationId,
            [new TypeIBaselineSourceReference("approved_boundary", boundaryId,
                Uuid.CreateVersion4(), 1, SourceHash1),
             new TypeIBaselineSourceReference("system_description", SourceRecord1,
                SourceVersion1, 3, SourceHash1),
             new TypeIBaselineSourceReference("system_description", SourceRecord2,
                SourceVersion2, 4, SourceHash2)]);
            Assert.Null(engagement.FreezeBaseline(engagement.Revision, BaselineId, preview,
                "Confirmed the exact Type I source snapshot.", memberId, "Engagement lead",
                Now.AddMinutes(1)));
            await ExaminationSeeds.WriteAsync(Reader, Writer, PeriodEvents, ContentStore, engagement);
            return engagement.Baseline!;
        }

        public async Task SeedBuiltPackageAsync(Uuid packageId, TypeIBaselineView baseline,
            string? firstSourceHash = null, bool storeArchive = true,
            bool corruptArchiveEntry = false,
            IReadOnlyList<AuditPackageSelectionEntry>? selectedContent = null)
        {
            var package = new AuditPackageAggregate(TenantId, ProgramId, packageId);
            var memberId = RbacIds.Member(TenantId, UserId);
            var content = Content(baseline, firstSourceHash) with
            { SelectedContent = selectedContent ?? Content(baseline, firstSourceHash).SelectedContent };
            Assert.Null(package.Create(content, memberId,
                "Package author", Now));
            Assert.Null(package.RequestBuild(package.Revision, "build-key-1", memberId,
                "Package author", Now.AddMinutes(2)));
            var manifest = package.Current!.Versions[0].Manifest;
            var attempt = Assert.Single(package.Current.Versions[0].BuildAttempts);
            var archiveBytes = await BuildArchiveAsync(manifest, corruptArchiveEntry);
            var archiveSha256 = Convert.ToHexStringLower(SHA256.HashData(archiveBytes));
            if (storeArchive)
            {
                var stored = await ArtifactStore.StoreIfAbsentAsync(TenantId,
                    new MemoryStream(archiveBytes));
                Assert.Equal(archiveSha256, stored.Content.Sha256);
                Assert.Equal(archiveBytes.LongLength, stored.Content.Length);
            }
            Assert.Null(package.RecordBuildResult(package.Revision, attempt.BuildAttemptId,
                new AuditPackageBuildOutput(manifest.AuditPackageManifestId,
                    manifest.ManifestSha256, manifest.Entries, ArchiveId, archiveSha256,
                    archiveBytes.LongLength, null), null, "build-worker", Now.AddMinutes(3)));
            await PackageSeeds.WriteAsync(Reader, Writer, PeriodEvents, ContentStore, package.Current!);
        }

        async Task<byte[]> BuildArchiveAsync(AuditPackageManifestView manifest,
            bool corruptFirstEntry)
        {
            using var buffer = new MemoryStream();
            using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                for (var index = 0; index < manifest.Entries.Count; index++)
                {
                    var manifestEntry = manifest.Entries[index];
                    var entry = archive.CreateEntry(manifestEntry.RelativePath,
                        CompressionLevel.NoCompression);
                    await using var stream = entry.Open();
                    var original = manifestEntry.Source.SourceRecordId == SourceRecord1
                        ? SourceBytes1 : SourceBytes2;
                    var content = original;
                    if (corruptFirstEntry && index == 0)
                    {
                        content = original.ToArray();
                        content[0] ^= 0xff;
                    }
                    await stream.WriteAsync(content);
                }
            }
            return buffer.ToArray();
        }

        public AuditPackageContent Content(TypeIBaselineView baseline, string? firstSourceHash = null) => new(ExaminationId,
            "type_i", "type_i_baseline", baseline.TypeIBaselineId, baseline.ContentSha256,
            "SOC 2 Type I", [
                Selection(SourceRecord1, SourceVersion1, 3, firstSourceHash ?? SourceHash1),
                Selection(SourceRecord2, SourceVersion2, 4, SourceHash2),
            ]);

        static AuditPackageSelectionEntry Selection(Uuid sourceId, Uuid sourceVersionId,
            long revision, string hash) =>
            new("system_description", "system-description", "system_description", sourceId,
                revision, "json", hash, 128, "management", sourceVersionId);

        public static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(Actor(userId),
                Invocation()));

        public static Cntryl.Portia.RequestContext<T> SystemContext<T>(T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(RequestActor.System,
                Invocation()));

        static HttpInvocation Invocation() => new("POST",
            "/api/v1/tenants/test/programs/test/audit-packages/test/amendments",
            "/api/v1/tenants/test/programs/test/audit-packages/test/amendments",
            "audit-package-amend-test");

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "package-author@example.test")], "BdgrzSession"));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            if (Directory.Exists(_artifactRoot))
                Directory.Delete(_artifactRoot, recursive: true);
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
