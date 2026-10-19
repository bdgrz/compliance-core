using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Readiness;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class ExaminationEngagementCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldRejectCreateGivenNoApprovedTypeIEntryDecision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = new CreateExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, "type_i", "Independent Audit LLP", new DateOnly(2026, 9, 30),
            null, ["security", "availability"], fixture.BoundaryId);

        // Act
        var result = await fixture.CreateHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ExaminationEngagement(fixture.TenantId,
            fixture.ProgramId, fixture.EngagementId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectCreateGivenDeferredTypeIEntryDecision()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedTypeIEntryDecisionAsync(ReadinessLedger.Defer);
        var request = new CreateExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, "type_i", "Independent Audit LLP", new DateOnly(2026, 9, 30),
            null, ["security", "availability"], fixture.BoundaryId);

        // Act
        var result = await fixture.CreateHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ExaminationEngagement(fixture.TenantId,
            fixture.ProgramId, fixture.EngagementId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldCreateTypeIEngagementGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedTypeIEntryDecisionAsync(ReadinessLedger.Approve);
        var request = new CreateExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, "type_i", "Independent Audit LLP", new DateOnly(2026, 9, 30),
            null, ["security", "availability"], fixture.BoundaryId);

        // Act
        var result = await fixture.CreateHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ExaminationEngagement(fixture.TenantId,
            fixture.ProgramId, fixture.EngagementId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("draft", saved.Current!.Status);
        Assert.Equal(1, saved.Revision);
        Assert.Equal("Independent Audit LLP", saved.Current.AuditFirm);
        Assert.Equal(fixture.BoundaryId, saved.Current.BoundaryId);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId), saved.Current.ActorMemberId);
    }

    [Fact]
    public async Task ShouldCreateTypeIEngagementGivenAuthenticatedMcpCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedTypeIEntryDecisionAsync(ReadinessLedger.Approve);
        var request = new CreateExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, "type_i", "Independent Audit LLP", new DateOnly(2026, 9, 30),
            null, ["security", "availability"], fixture.BoundaryId);

        // Act
        var result = await fixture.CreateHandler().HandleAsync(fixture.Context(request,
            new McpInvocation("bdgrz.examinations.create-examination-engagement")),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ExaminationEngagement(fixture.TenantId,
            fixture.ProgramId, fixture.EngagementId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("draft", saved.Current?.Status);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId), saved.Current?.ActorMemberId);
    }

    [Fact]
    public async Task ShouldCaptureActiveManagementContactsGivenCreate()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedTypeIEntryDecisionAsync(ReadinessLedger.Approve);
        var contactUserId = Uuid.CreateVersion4();
        var contactMemberId = RbacIds.Member(fixture.TenantId, contactUserId);
        await fixture.SeedMemberAsync(contactUserId, "client_personnel");
        var request = new CreateExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, "type_i", "Independent Audit LLP", new DateOnly(2026, 9, 30),
            null, ["security", "availability"], fixture.BoundaryId,
            ManagementContactMemberIds: [contactMemberId]);

        // Act
        var result = await fixture.CreateHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ExaminationEngagement(fixture.TenantId,
            fixture.ProgramId, fixture.EngagementId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal([contactMemberId], saved.Current!.ManagementContactMemberIds);
    }

    [Fact]
    public async Task ShouldRejectInactiveManagementContactGivenCreate()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedTypeIEntryDecisionAsync(ReadinessLedger.Approve);
        var contactUserId = Uuid.CreateVersion4();
        var contactMemberId = RbacIds.Member(fixture.TenantId, contactUserId);
        await fixture.SeedMemberAsync(contactUserId, "firm_staff");
        var request = new CreateExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, "type_i", "Independent Audit LLP", new DateOnly(2026, 9, 30),
            null, ["security", "availability"], fixture.BoundaryId,
            ManagementContactMemberIds: [contactMemberId]);

        // Act
        var result = await fixture.CreateHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ExaminationEngagement(fixture.TenantId,
            fixture.ProgramId, fixture.EngagementId));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldReviseDraftAtExpectedRevisionGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var request = new ReviseExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, 1, "Updated Independent Audit LLP",
            new DateOnly(2026, 9, 30), null, ["security"]);

        // Act
        var result = await fixture.ReviseHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ExaminationEngagement(fixture.TenantId,
            fixture.ProgramId, fixture.EngagementId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Updated Independent Audit LLP", saved.Current!.AuditFirm);
        Assert.Equal(2, saved.Revision);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId), saved.Current.ActorMemberId);
    }

    [Fact]
    public async Task ShouldReplaceManagementContactsGivenActiveClientMembers()
    {
        // Arrange
        await using var fixture = new Fixture();
        var previousUserId = Uuid.CreateVersion4();
        var replacementUserId = Uuid.CreateVersion4();
        var previousMemberId = RbacIds.Member(fixture.TenantId, previousUserId);
        var replacementMemberId = RbacIds.Member(fixture.TenantId, replacementUserId);
        await fixture.SeedMemberAsync(previousUserId, "client_personnel");
        await fixture.SeedMemberAsync(replacementUserId, "client_personnel");
        await fixture.SeedEngagementAsync([previousMemberId]);
        var request = new ReviseExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, 1, "Updated Independent Audit LLP",
            new DateOnly(2026, 9, 30), null, ["security"],
            ManagementContactMemberIds: [replacementMemberId]);

        // Act
        var result = await fixture.ReviseHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ExaminationEngagement(fixture.TenantId,
            fixture.ProgramId, fixture.EngagementId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal([replacementMemberId], saved.Current!.ManagementContactMemberIds);
    }

    [Fact]
    public async Task ShouldPreserveManagementContactsGivenRevisionWithoutContactField()
    {
        // Arrange
        await using var fixture = new Fixture();
        var contactUserId = Uuid.CreateVersion4();
        var contactMemberId = RbacIds.Member(fixture.TenantId, contactUserId);
        await fixture.SeedMemberAsync(contactUserId, "client_personnel");
        await fixture.SeedEngagementAsync([contactMemberId]);
        var request = new ReviseExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, 1, "Updated Independent Audit LLP",
            new DateOnly(2026, 9, 30), null, ["security"]);

        // Act
        var result = await fixture.ReviseHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ExaminationEngagement(fixture.TenantId,
            fixture.ProgramId, fixture.EngagementId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal([contactMemberId], saved.Current!.ManagementContactMemberIds);
    }

    [Fact]
    public async Task ShouldReadCurrentEngagementGivenTenantProgramAndEngagementIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var request = new GetExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId);

        // Act
        var result = await new GetExaminationEngagementHandler(fixture.Directory)
            .HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fixture.TenantId, result.Value.TenantId);
        Assert.Equal(fixture.ProgramId, result.Value.ProgramId);
        Assert.Equal(fixture.EngagementId, result.Value.ExaminationEngagementId);
        Assert.Equal("draft", result.Value.Status);
        Assert.Equal(1, result.Value.Revision);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenUnknownEngagement()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = new GetExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId);

        // Act
        var result = await new GetExaminationEngagementHandler(fixture.Directory)
            .HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenEngagementFromDifferentProgram()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var request = new GetExaminationEngagement(fixture.TenantId, Uuid.CreateVersion4(),
            fixture.EngagementId);

        // Act
        var result = await new GetExaminationEngagementHandler(fixture.Directory)
            .HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReadFrozenBaselineGivenTenantProgramAndBaselineIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedBaselineAsync();
        var request = new GetTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.BaselineId);

        // Act
        var result = await new GetTypeIBaselineHandler(fixture.Directory)
            .HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fixture.TenantId, result.Value.TenantId);
        Assert.Equal(fixture.ProgramId, result.Value.ProgramId);
        Assert.Equal(fixture.EngagementId, result.Value.ExaminationEngagementId);
        Assert.Equal(fixture.BaselineId, result.Value.TypeIBaselineId);
        Assert.Equal("frozen", result.Value.Status);
        Assert.Equal(1, result.Value.Revision);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenBaselineFromDifferentProgram()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedBaselineAsync();
        var request = new GetTypeIBaseline(fixture.TenantId, Uuid.CreateVersion4(),
            fixture.BaselineId);

        // Act
        var result = await new GetTypeIBaselineHandler(fixture.Directory)
            .HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectStaleRevisionWithoutAppendingGivenDraftRevision()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var request = new ReviseExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, 0, "Stale change", new DateOnly(2026, 9, 30), null,
            ["security"]);

        // Act
        var result = await fixture.ReviseHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ExaminationEngagement(fixture.TenantId,
            fixture.ProgramId, fixture.EngagementId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldRequirePersonalHttpActorGivenCreateCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = new CreateExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, "type_i", "Independent Audit LLP", new DateOnly(2026, 9, 30),
            null, ["security"], fixture.BoundaryId);
        var context = new Cntryl.Portia.RequestContext<CreateExaminationEngagement>(request,
            new RequestDispatchContext(RequestActor.System,
                new HttpInvocation("POST", "/examinations", "/examinations", "examination-test")));

        // Act
        var result = await fixture.CreateHandler().HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ExaminationEngagement(fixture.TenantId,
            fixture.ProgramId, fixture.EngagementId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(0, saved.Revision);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid EngagementId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineId { get; } = Uuid.CreateVersion4();
        public Uuid BoundaryId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        public FitzExaminationEngagementDirectory Directory { get; }
        public TimeProvider Clock { get; } = new FixedTimeProvider(Now);

        ClaimsPrincipal Actor => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()),
                new Claim("email", "examiner@example.test")], "BdgrzSession"));

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            Directory = new FitzExaminationEngagementDirectory(new InMemoryKvClient());
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public CreateExaminationEngagementHandler CreateHandler() => new(Reader, Executor, Clock);

        public ReviseExaminationEngagementHandler ReviseHandler() => new(Reader, Executor, Clock);

        public Cntryl.Portia.RequestContext<T> Context<T>(T request,
            RequestInvocation? invocation = null) where T : IRequestBase => new(request,
            new RequestDispatchContext(Actor, invocation ?? new HttpInvocation("POST",
                "/api/v1/examinations", "/api/v1/examinations", "examination-engagement-test")));

        public async Task SeedEngagementAsync(IReadOnlyList<Uuid>? managementContactMemberIds = null)
        {
            var aggregate = new ExaminationEngagement(TenantId, ProgramId, EngagementId);
            Assert.Null(aggregate.Create(new ExaminationEngagementContent("type_i", "Original LLP",
                new DateOnly(2026, 9, 30), null, ["security"], BoundaryId,
                managementContactMemberIds ?? []),
                RbacIds.Member(TenantId, UserId), "Examiner", Now));
            await Writer.SaveAsync(aggregate, new RequestDispatchContext(RequestActor.System));
            await ProjectAsync(new ExaminationEngagementOpened(TenantId,
                aggregate.Current!, Now));
        }

        public async Task SeedMemberAsync(Uuid userId, string affiliation)
        {
            var member = new Member(TenantId, userId);
            Assert.True(member.Register(affiliation).IsSuccess);
            await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        }

        public async Task SeedTypeIEntryDecisionAsync(string outcome)
        {
            var assessmentId = Uuid.CreateVersion4();
            var runnerMemberId = RbacIds.Member(TenantId, UserId);
            var approverMemberId = RbacIds.Member(TenantId, Uuid.CreateVersion4());
            var ledger = new ReadinessLedger(TenantId, ProgramId);
            Assert.Null(ledger.Record(0, assessmentId, Now, null,
                new ReadinessEvaluation([], [], [], new string('a', 64)), runnerMemberId,
                "Readiness runner", Now));
            if (outcome != ReadinessLedger.Defer)
                Assert.Null(ledger.Decide(assessmentId, ledger.Revision, Uuid.CreateVersion4(),
                    ReadinessLedger.Proceed, "Management decided to proceed", approverMemberId,
                    "Management approver", Now));
            Assert.Null(ledger.DecideTypeIEntry(assessmentId, ledger.Revision,
                Uuid.CreateVersion4(), outcome, "Type I entry decision", [], approverMemberId,
                "Management approver", Now));
            await Writer.SaveAsync(ledger, new RequestDispatchContext(RequestActor.System));
        }

        public async Task SeedBaselineAsync()
        {
            await SeedEngagementAsync();
            var engagement = await Directory.GetAsync(TenantId, EngagementId);
            var source = new TypeIBaselineSourceReference("approved_boundary", BoundaryId,
                Uuid.CreateVersion4(), 1, new string('b', 64));
            var preview = TypeIBaselinePreviewTestFactory.Create(TenantId, ProgramId,
                EngagementId, [source]);
            var frozenAt = Now.AddMinutes(1);
            var baseline = new TypeIBaselineView(TenantId, ProgramId, EngagementId, BaselineId,
                "frozen", 1, preview.Sources, [], preview.CanonicalManifest, preview.ContentSha256,
                frozenAt, RbacIds.Member(TenantId, UserId), "Examiner", "Confirmed", null,
                null, [], [], 0)
            { SourceCoverage = preview.SourceCoverage };
            var current = engagement ?? throw new InvalidOperationException(
                "The seeded examination engagement was not projected.");
            var frozenEngagement = current with
            {
                Status = "baseline_frozen",
                Revision = current.Revision + 1,
                UpdatedAt = frozenAt,
            };
            await ProjectAsync(new TypeIBaselineFrozen(TenantId, frozenEngagement, baseline,
                frozenAt));
        }

        async Task ProjectAsync(DomainEvent domainEvent)
        {
            var identity = new CheckpointIdentity(FitzExaminationEngagementDirectory.ProjectorName,
                EventStreamPattern.ForPattern(TenantId.ToString()));
            await using var batch = await Directory.BeginAsync(new ProjectionBatchContext(identity,
                ProjectionCheckpoint.Start));
            await Directory.ApplyAsync(domainEvent);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
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
}
