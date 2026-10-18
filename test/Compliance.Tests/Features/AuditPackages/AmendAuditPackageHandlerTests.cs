using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using AuditPackageAggregate = Bdgrz.Compliance.Features.AuditPackages.AuditPackage;

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
        var request = new AmendAuditPackage(fixture.TenantId, fixture.ProgramId, packageId, 3,
            "Remove the superseded source record.", [fixture.SourceRecord1]);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.UserId,
            request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(fixture.TenantId,
            fixture.ProgramId, packageId));

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
        var request = new AmendAuditPackage(fixture.TenantId, fixture.ProgramId, packageId, 3,
            "Attempt to add an unselected source.", [Uuid.CreateVersion4()]);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.UserId,
            request), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(fixture.TenantId,
            fixture.ProgramId, packageId));

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
        var request = new AmendAuditPackage(fixture.TenantId, fixture.ProgramId,
            Uuid.CreateVersion4(), 3, "Unauthorized change.", []);

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
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        public IArtifactContentStore ArtifactStore { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            ArtifactStore = new LocalArtifactContentStore(new ArtifactContentStoreOptions(
                _artifactRoot, null, ArtifactContentLimits.MaxContentLength), _clock);
            services.AddSingleton(ArtifactStore);
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public AmendAuditPackageHandler Handler() => new(Executor, Reader,
            new AuditPackageSourceBuilder(Reader), _clock);

        public async Task<TypeIBaselineView> SeedBaselineAsync()
        {
            var memberId = RbacIds.Member(TenantId, UserId);
            var engagement = new ExaminationEngagement(TenantId, ProgramId, ExaminationId);
            Assert.Null(engagement.Create(new ExaminationEngagementContent("type_i",
                "Independent Audit LLP", new DateOnly(2026, 9, 30), null, ["security"],
                Uuid.CreateVersion4(), [memberId]), memberId, "Engagement lead", Now));
            var preview = TypeIBaselinePreview.Create(TenantId, ProgramId, ExaminationId,
            [new TypeIBaselineSourceReference("system_description", SourceRecord1,
                SourceVersion1, 3, SourceHash1),
             new TypeIBaselineSourceReference("system_description", SourceRecord2,
                SourceVersion2, 4, SourceHash2)]);
            Assert.Null(engagement.FreezeBaseline(engagement.Revision, BaselineId, preview,
                "Confirmed the exact Type I source snapshot.", memberId, "Engagement lead",
                Now.AddMinutes(1)));
            await Writer.SaveAsync(engagement, new RequestDispatchContext(RequestActor.System));
            return engagement.Baseline!;
        }

        public async Task SeedBuiltPackageAsync(Uuid packageId, TypeIBaselineView baseline,
            string? firstSourceHash = null, bool storeArchive = true,
            bool corruptArchiveEntry = false)
        {
            var package = new AuditPackageAggregate(TenantId, ProgramId, packageId);
            var memberId = RbacIds.Member(TenantId, UserId);
            Assert.Null(package.Create(Content(baseline, firstSourceHash), memberId,
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
            await Writer.SaveAsync(package, new RequestDispatchContext(RequestActor.System));
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

        AuditPackageContent Content(TypeIBaselineView baseline, string? firstSourceHash = null) => new(ExaminationId,
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
