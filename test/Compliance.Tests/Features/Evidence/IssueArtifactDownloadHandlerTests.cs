using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Retention;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

public sealed class IssueArtifactDownloadHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldIssueShortLivedAttributedDeliveryGivenAvailableExactArtifactGrant()
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Clean);
        var request = new IssueArtifactDownload(fixture.TenantId, artifactId, "Type I audit review");
        var handler = fixture.Handler();

        // Act
        var result = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var retained = await fixture.Reader.HydrateAsync(new EvidenceArtifact(fixture.TenantId, artifactId));
        var events = await fixture.ArtifactEventsAsync(artifactId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(Now.AddMinutes(5), result.Value.ExpiresAt);
        Assert.StartsWith("urn:bdgrz:artifact-delivery:", result.Value.Location.ToString());
        Assert.Equal(3, retained.Revision);
        Assert.Equal(3UL, retained.CommittedStreamPosition);
        var issued = Assert.IsType<EvidenceArtifactDownloadIssued>(events[^1]);
        Assert.Equal(fixture.TenantId, issued.TenantId);
        Assert.Equal(artifactId, issued.ArtifactId);
        Assert.Equal("Type I audit review", issued.Purpose);
        Assert.Equal(ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId),
            "lead@example.test"), issued.IssuedBy);
    }

    [Theory]
    [InlineData(EvidenceInspectionOutcome.Malware)]
    [InlineData(EvidenceInspectionOutcome.SecretDetected)]
    public async Task ShouldRefuseDeliveryWithoutAppendGivenUnavailableArtifact(EvidenceInspectionOutcome outcome)
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifactId = await fixture.SeedArtifactAsync(outcome);
        var request = new IssueArtifactDownload(fixture.TenantId, artifactId, "Type I audit review");

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None);
        var retained = await fixture.Reader.HydrateAsync(new EvidenceArtifact(fixture.TenantId, artifactId));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(2, retained.Revision);
        Assert.Equal(2UL, retained.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRequirePersonalHttpInvocationGivenDownloadRequest()
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Clean);
        var request = new IssueArtifactDownload(fixture.TenantId, artifactId, "Type I audit review");
        var context = new Cntryl.Portia.RequestContext<IssueArtifactDownload>(request,
            new RequestDispatchContext(RequestActor.System, new HttpInvocation("POST", "/test", "/test", "test")));

        // Act
        var result = await fixture.Handler().HandleAsync(context, CancellationToken.None);
        var retained = await fixture.Reader.HydrateAsync(new EvidenceArtifact(fixture.TenantId, artifactId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(2UL, retained.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRequireSeparateArtifactGrantGivenKnownContentIdentity()
    {
        // Arrange
        await using var fixture = new Fixture(allow: false);
        var artifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Clean);
        var request = new IssueArtifactDownload(fixture.TenantId, artifactId, "Type I audit review");

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None);
        var retained = await fixture.Reader.HydrateAsync(new EvidenceArtifact(fixture.TenantId, artifactId));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(2UL, retained.CommittedStreamPosition);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly LocalArtifactContentStore _store;
        readonly string _root = Path.Combine(Path.GetTempPath(), $"bdgrz-evidence-download-{Guid.NewGuid():N}");
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public TimeProvider Clock { get; } = new FixedTimeProvider(Now);
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        public IDomainEventReader Events { get; }
        public EvidenceArtifactDownloadAccess Access { get; }
        ClaimsPrincipal Actor => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()),
                new Claim("email", "lead@example.test")], "BdgrzSession"));

        public Fixture(bool allow = true)
        {
            var services = new ServiceCollection();
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            Events = _scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
            _store = new LocalArtifactContentStore(new ArtifactContentStoreOptions(_root, new byte[32],
                ArtifactContentLimits.MaxContentLength), Clock);
            Access = new(new FixedArtifactPermission(allow));
        }

        public IssueArtifactDownloadHandler Handler() => new(Access,
                new EvidenceArtifactMetadataRead(Reader, Events), _store, Executor, Clock);

        public Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request,
            new RequestDispatchContext(Actor, new HttpInvocation("POST", "/api/v1/evidence/downloads",
                "/api/v1/evidence/downloads", "evidence-download-test")));

        public async Task<Uuid> SeedArtifactAsync(EvidenceInspectionOutcome outcome)
        {
            var bytes = "verified evidence"u8.ToArray();
            var stored = await _store.StoreIfAbsentAsync(TenantId, new MemoryStream(bytes));
            var artifactId = EvidenceIntake.IdFor(TenantId, stored.Content.Sha256);
            var artifact = new EvidenceArtifact(TenantId, artifactId);
            Assert.True(artifact.Register(new EvidenceArtifactContent("Audit evidence", null, "document",
                    "Manual upload", Now.AddMinutes(-2), new DateOnly(2025, 1, 1),
                    new DateOnly(2025, 12, 31), "restricted"),
                stored.Content.Sha256, stored.Content.Length,
                ActorReference.ForMember(RbacIds.Member(TenantId, UserId), "Collector"),
                Now.AddMinutes(-2)).IsSuccess);
            Assert.Null(artifact.RecordInspection(outcome, Now.AddMinutes(-1)));
            await Writer.SaveAsync(artifact, Context(new GetEvidenceArtifactMetadata(TenantId, artifactId)));
            if (outcome == EvidenceInspectionOutcome.Malware)
                Assert.True(await _store.QuarantineAsync(stored.Content));
            if (outcome is EvidenceInspectionOutcome.SecretDetected or EvidenceInspectionOutcome.Invalid)
                Assert.Equal(ArtifactDeletionResult.Deleted, await _store.DeleteAsync(stored.Content));
            return artifactId;
        }

        public async Task<IReadOnlyList<DomainEvent>> ArtifactEventsAsync(Uuid artifactId)
        {
            var list = new List<DomainEvent>();
            await foreach (var record in Events.ReadAsync(EvidenceStreams.Address(TenantId, artifactId)))
                list.Add(record.Event);
            return list;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    sealed class FixedArtifactPermission(bool allow) : IAccessGrantScopePermissionAuthorizer
    {
        readonly bool _allow = allow;

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, Uuid programId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(_allow);

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new ProgramAccessVisibility(_allow, new HashSet<Uuid>()));

        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            IReadOnlyCollection<AccessGrantScope> scopes, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(_allow && tenantId != Uuid.Empty && userId != Uuid.Empty &&
                memberId == RbacIds.Member(tenantId, userId) && scopes.Count == 1 &&
                scopes.Single() is
                {
                    Kind: AccessGrantScopeKind.SharedResource,
                    ResourceType: EvidenceArtifactResourceTypes.Artifact,
                    Id: var id
                } && id != Uuid.Empty &&
                permission == RbacPermissions.EvidenceArtifactRead);

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, string permission, CancellationToken ct = default) => ValueTask.FromResult(_allow);
    }
}
