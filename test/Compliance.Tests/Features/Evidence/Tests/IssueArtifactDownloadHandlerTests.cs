using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Artifacts.Models;
using Bdgrz.Compliance.Features.Artifacts.Persistence;
using Bdgrz.Compliance.Features.Artifacts.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Responsibilities.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence.Handlers;
using Bdgrz.Compliance.Features.Evidence.Queries;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Evidence.Tests;

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

    [Fact]
    public async Task ShouldIssueSelectedEvidenceDownloadGivenActiveAdvisorHandoff()
    {
        // Arrange
        await using var fixture = new Fixture(allow: false);
        var artifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Clean);
        var metadata = await new EvidenceArtifactMetadataRead(fixture.Reader, fixture.Events)
            .GetAsync(fixture.TenantId, artifactId, CancellationToken.None);
        Assert.True(metadata.IsSuccess, metadata.Error?.Message);
        var selected = AdvisorRecordVersion.EvidenceArtifact(artifactId, revision: 2,
            metadata.Value.SourcePosition, metadata.Value.ContentSha256, Uuid.CreateVersion4());
        var request = new IssueArtifactDownload(fixture.TenantId, artifactId,
            "Advisor review")
        {
            ProgramId = Uuid.CreateVersion4(),
            AdvisorHandoffId = Uuid.CreateVersion4(),
            SelectedSource = selected,
        };
        var sharedAccess = new EvidenceArtifactDownloadAccess(new FixedArtifactPermission(false),
            new FixedAdvisorSourceAuthorization(allow: true),
            new FixedCurrentSourceResolver());

        // Act
        var result = await fixture.Handler(sharedAccess).HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var retained = await fixture.Reader.HydrateAsync(new EvidenceArtifact(fixture.TenantId,
            artifactId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(3, retained.Revision);
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

        public IssueArtifactDownloadHandler Handler(EvidenceArtifactDownloadAccess? access = null) => new(access ?? Access,
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

    sealed class FixedAdvisorSourceAuthorization(bool allow) : IAdvisorSharedSourceAuthorization
    {
        public ValueTask<bool> CanReadSelectedSourceAsync(Uuid tenantId, Uuid programId,
            Uuid handoffId, AdvisorRecordVersion sourceVersion, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult(allow);
    }

    sealed class FixedCurrentSourceResolver : IAdvisorRecordVersionResolver
    {
        public ValueTask<Result> RequireCurrentAsync(Uuid tenantId, Uuid programId,
            IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default) =>
            ValueTask.FromResult(Result.Success);
    }
}
