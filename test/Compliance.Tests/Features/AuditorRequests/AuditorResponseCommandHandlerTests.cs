using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

public sealed class AuditorResponseCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldCreateReviseAndSubmitResponseGivenPersonalCommands()
    {
        // Arrange
        await using var fixture = new Fixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();

        // Act
        var created = await fixture.CreateDraftHandler().HandleAsync(fixture.Context(
            new CreateAuditResponseDraft(fixture.TenantId, fixture.ProgramId, requestId,
                responseId, "Initial evidence explanation", [])), CancellationToken.None);
        var revised = await fixture.ReviseDraftHandler().HandleAsync(fixture.Context(
            new ReviseAuditResponseDraft(fixture.TenantId, fixture.ProgramId, requestId,
                responseId, 2, "Revised evidence explanation", [])), CancellationToken.None);
        var submitted = await fixture.SubmitHandler().HandleAsync(fixture.Context(
            new SubmitAuditResponse(fixture.TenantId, fixture.ProgramId, requestId, responseId,
                2, "Ready for independent review")), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditorRequest(fixture.TenantId,
            fixture.ProgramId, requestId));

        // Assert
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        Assert.True(submitted.IsSuccess, submitted.Error?.Message);
        var response = Assert.Single(saved.Current!.Responses);
        Assert.Equal(2, response.CurrentVersion);
        Assert.Equal("submitted", response.Status);
        Assert.Equal("Revised evidence explanation", response.Versions[^1].Summary);
    }

    [Fact]
    public async Task ShouldRequireReadableAvailableEvidenceGivenResponseDraft()
    {
        // Arrange
        await using var fixture = new Fixture();
        var requestId = await fixture.SeedRequestAsync();
        var unavailableArtifact = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Malware);
        var command = new CreateAuditResponseDraft(fixture.TenantId, fixture.ProgramId, requestId,
            Uuid.CreateVersion4(), "Evidence requiring review", [unavailableArtifact]);

        // Act
        var result = await fixture.CreateDraftHandler().HandleAsync(fixture.Context(command),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditorRequest(fixture.TenantId,
            fixture.ProgramId, requestId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(saved.Current!.Responses);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldRequireExactArtifactReadGrantGivenResponseDraft()
    {
        // Arrange
        await using var fixture = new Fixture(allowArtifactRead: false);
        var requestId = await fixture.SeedRequestAsync();
        var artifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Clean);
        var command = new CreateAuditResponseDraft(fixture.TenantId, fixture.ProgramId, requestId,
            Uuid.CreateVersion4(), "Evidence requiring review", [artifactId]);

        // Act
        var result = await fixture.CreateDraftHandler().HandleAsync(fixture.Context(command),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditorRequest(fixture.TenantId,
            fixture.ProgramId, requestId));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(saved.Current!.Responses);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldReviewDeliverAcceptAndResolveResponseGivenIndependentReviewer()
    {
        // Arrange
        await using var fixture = new Fixture();
        var requestId = await fixture.SeedRequestAsync();
        var responseId = Uuid.CreateVersion4();
        var create = await fixture.CreateDraftHandler().HandleAsync(fixture.Context(
            new CreateAuditResponseDraft(fixture.TenantId, fixture.ProgramId, requestId,
                responseId, "Complete response", [])), CancellationToken.None);
        Assert.True(create.IsSuccess, create.Error?.Message);
        var submit = await fixture.SubmitHandler().HandleAsync(fixture.Context(
            new SubmitAuditResponse(fixture.TenantId, fixture.ProgramId, requestId, responseId,
                1, "Submitted for review")), CancellationToken.None);
        Assert.True(submit.IsSuccess, submit.Error?.Message);

        // Act
        var authorReview = await fixture.ReviewHandler().HandleAsync(fixture.Context(
            new ReviewAuditResponse(fixture.TenantId, fixture.ProgramId, requestId, responseId,
                1, "approved", "Sufficient response")), CancellationToken.None);
        var independentReview = await fixture.ReviewHandler().HandleAsync(Fixture.ContextFor(
            fixture.ReviewerId, new ReviewAuditResponse(fixture.TenantId, fixture.ProgramId,
                requestId, responseId, 1, "approved", "Sufficient response")), CancellationToken.None);
        var delivered = await fixture.DeliverHandler().HandleAsync(fixture.Context(
            new DeliverAuditResponse(fixture.TenantId, fixture.ProgramId, requestId, responseId,
                1, "External audit team", "delivery-2026-001")), CancellationToken.None);
        var disposition = await fixture.DispositionHandler().HandleAsync(fixture.Context(
            new RecordAuditorResponseDisposition(fixture.TenantId, fixture.ProgramId, requestId,
                responseId, 1, "accepted", "audit-portal-accepted")), CancellationToken.None);
        var resolved = await fixture.ResolveHandler().HandleAsync(fixture.Context(
            new ResolveAuditorRequest(fixture.TenantId, fixture.ProgramId, requestId,
                "The delivered response was accepted.")), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditorRequest(fixture.TenantId,
            fixture.ProgramId, requestId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, authorReview.Error?.Kind);
        Assert.True(independentReview.IsSuccess, independentReview.Error?.Message);
        Assert.True(delivered.IsSuccess, delivered.Error?.Message);
        Assert.True(disposition.IsSuccess, disposition.Error?.Message);
        Assert.True(resolved.IsSuccess, resolved.Error?.Message);
        Assert.Equal("resolved", saved.Current!.Status);
        Assert.Equal("accepted", Assert.Single(saved.Current.Responses).Disposition!.Outcome);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly TimeProvider _clock = new FixedTimeProvider(Now);
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        readonly EvidenceArtifactDownloadAccess _access;
        readonly EvidenceArtifactMetadataRead _metadata;

        static ClaimsPrincipal ActorFor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "attest@example.test")], "BdgrzSession"));

        public Fixture(bool allowArtifactRead = true)
        {
            var store = new InMemoryEventStore();
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            _access = new(new FixedArtifactPermission(allowArtifactRead));
            _metadata = new(Reader, _scope.ServiceProvider.GetRequiredService<IDomainEventReader>());
        }

        public CreateAuditResponseDraftHandler CreateDraftHandler() => new(Executor,
            new(_access, _metadata, _clock), _clock);

        public ReviseAuditResponseDraftHandler ReviseDraftHandler() => new(Executor,
            new(_access, _metadata, _clock), _clock);

        public SubmitAuditResponseHandler SubmitHandler() => new(Executor, _clock);
        public ReviewAuditResponseHandler ReviewHandler() => new(Executor, _clock);
        public DeliverAuditResponseHandler DeliverHandler() => new(Executor, _clock);
        public RecordAuditorResponseDispositionHandler DispositionHandler() => new(Executor, _clock);
        public ResolveAuditorRequestHandler ResolveHandler() => new(Executor, _clock);

        public Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase =>
            ContextFor(UserId, request);

        public static Cntryl.Portia.RequestContext<T> ContextFor<T>(Uuid userId, T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(ActorFor(userId),
            new HttpInvocation("POST", "/api/v1/auditor-requests", "/api/v1/auditor-requests",
                "auditor-response-handler-test")));

        public async Task<Uuid> SeedRequestAsync()
        {
            await SeedMemberAsync(UserId);
            var requestId = Uuid.CreateVersion4();
            var aggregate = new AuditorRequest(TenantId, ProgramId, requestId);
            Assert.Null(aggregate.Create(new AuditorRequestContent(ExaminationId, "external_email",
                "mail-seed", "Request wording", "The complete evidence response", "normal",
                RbacIds.Member(TenantId, UserId), new DateOnly(2026, 10, 31), true),
                RbacIds.Member(TenantId, UserId), "attest@example.test", Now));
            await Writer.SaveAsync(aggregate, new RequestDispatchContext(RequestActor.System));
            return requestId;
        }

        public async Task<Uuid> SeedArtifactAsync(EvidenceInspectionOutcome outcome)
        {
            var artifactId = Uuid.CreateVersion4();
            var bytes = Encoding.UTF8.GetBytes("auditor response evidence");
            var digest = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var artifact = new EvidenceArtifact(TenantId, artifactId);
            var collector = ActorReference.ForMember(RbacIds.Member(TenantId, UserId),
                "attest@example.test");
            Assert.True(artifact.Register(new EvidenceArtifactContent("Audit evidence", null,
                    "document", "Manual upload", Now.AddMinutes(-5), new DateOnly(2025, 1, 1),
                    new DateOnly(2025, 12, 31), "restricted"), digest, bytes.Length, collector,
                Now.AddMinutes(-5)).IsSuccess);
            Assert.Null(artifact.RecordInspection(outcome, Now.AddMinutes(-1)));
            await Writer.SaveAsync(artifact, new RequestDispatchContext(RequestActor.System));
            return artifactId;
        }

        async Task SeedMemberAsync(Uuid userId)
        {
            var member = new Member(TenantId, userId);
            Assert.True(member.Register().IsSuccess);
            await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    sealed class FixedArtifactPermission(bool allowed) : IAccessGrantScopePermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(allowed);

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new ProgramAccessVisibility(allowed, new HashSet<Uuid>()));

        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, IReadOnlyCollection<AccessGrantScope> scopes, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(allowed &&
            tenantId != Uuid.Empty && userId != Uuid.Empty && memberId == RbacIds.Member(tenantId,
                userId) && scopes.Count == 1 && scopes.Single() is
                {
                    Kind: AccessGrantScopeKind.SharedResource, ResourceType: EvidenceArtifactResourceTypes.Artifact,
                    Id: var artifactId
                } && artifactId != Uuid.Empty &&
            permission == RbacPermissions.EvidenceArtifactRead);

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(false);
    }
}
