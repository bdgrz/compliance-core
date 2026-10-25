using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Commitments;

public sealed class PersonalCommitmentDecisionTransportTests
{
    [Fact]
    public async Task ShouldReplayOriginalRetirementGivenSameCommandAndLaterServerClock()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(approval: true);
        Assert.True((await fixture.DecideAsync(approval: true, "http")).IsSuccess);
        var commandId = Uuid.CreateVersion4();
        var request = new RetireCommitment(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.DraftId, 1,
            new DateOnly(2027, 1, 1), "The original accepted retirement.");
        var first = await IdentifiedRetirementAsync(fixture.Provider, fixture.Approver, request, commandId);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var accepted = await fixture.ReadAsync();
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(1);

        // Act
        var replay = await IdentifiedRetirementAsync(fixture.Provider, fixture.Approver, request, commandId);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(accepted.SourcePhysicalPosition, retained.SourcePhysicalPosition);
        Assert.Equal(accepted.RetiredEffectiveOn, retained.RetiredEffectiveOn);
        Assert.Equal(accepted.RetirementRationale, retained.RetirementRationale);
    }

    static async Task<Result> IdentifiedRetirementAsync(IServiceProvider provider, Uuid user, RetireCommitment request, Uuid commandId)
    {
        request = request with { CommandId = commandId };
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(user),
                new HttpInvocation("POST", "/synthetic/commitment/retirement", "/synthetic/commitment/retirement", "synthetic"),
                new RequestMetadata(commandId, commandId)), CancellationToken.None);
    }

    [Theory]
    [InlineData(false, "direct")]
    [InlineData(false, "mcp")]
    [InlineData(true, "direct")]
    [InlineData(true, "mcp")]
    public async Task ShouldRefusePersonalCommitmentDecisionGivenNonHttpInvocation(bool approval, string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(approval);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(approval, transport);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before.SourcePhysicalPosition, after.SourcePhysicalPosition);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.AcceptedReviewDecisionId, after.AcceptedReviewDecisionId);
        Assert.Equal(before.EffectiveVersionCount, after.EffectiveVersionCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetainAttributedCommitmentDecisionGivenNativeHttp(bool approval)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(approval);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(approval, "http");
        var after = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(before.SourcePhysicalPosition + 1, after.SourcePhysicalPosition);
        var events = await fixture.ReadEventsAsync();
        Assert.Equal(approval ? 1 : 0, after.EffectiveVersionCount);
        if (approval)
        {
            var decision = Assert.IsType<CommitmentApproved>(events[^1]);
            Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.Approver).ToString(), decision.Actor.Id);
            Assert.Equal(fixture.ReviewId, decision.AcceptedReviewDecisionId);
        }
        else
        {
            var decision = Assert.IsType<CommitmentReviewed>(events[^1]);
            Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.Reviewer).ToString(), decision.Actor.Id);
            Assert.Equal("verified", decision.SourceVerification);
            Assert.Equal("MSA 4.1", Assert.IsType<CommitmentReviewedContent>(Assert.Single((await fixture.ReadChangeAsync(decision)).Changes)).SourceVerifiedReference);
            Assert.Null(decision.Version);
        }
    }

    [Theory]
    [InlineData(false, "author", "A commitment author cannot review a revision they authored.")]
    [InlineData(true, "author", "A commitment author cannot approve a revision they authored.")]
    [InlineData(true, "reviewer", "The accepted reviewer cannot also approve the same revision.")]
    public async Task ShouldPreserveCommitmentSeparationGivenNativeHttpConflictingActor(bool approval, string actor, string message)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(approval);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(approval, "http", actor == "author" ? fixture.Author : fixture.Reviewer);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(message, result.Error!.Message);
        Assert.Equal(before.SourcePhysicalPosition, after.SourcePhysicalPosition);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveAssignedParticipantGivenNativeHttpUnassignedManager(bool approval)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(approval, assigned: true);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(approval, "http", fixture.Other);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(approval ? "Only an assigned approver of this exact revision can approve it." :
            "Only an assigned reviewer of this exact revision can review it.", result.Error!.Message);
        Assert.Equal(before.SourcePhysicalPosition, after.SourcePhysicalPosition);
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    public async Task ShouldRefusePersonalCommitmentRetirementGivenNonHttpInvocation(string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(approval: true);
        var approval = await fixture.DecideAsync(approval: true, "http");
        Assert.True(approval.IsSuccess, approval.Error?.Message);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.RetireAsync(transport);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before.SourcePhysicalPosition, after.SourcePhysicalPosition);
        Assert.False(after.IsRetired);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayOneCommitmentDecisionGivenSameCommandAndOriginalRevision(bool approval)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(approval);
        var commandId = Uuid.CreateVersion4();
        var digest = "unused";
        if (approval)
        {
            await using var scope = fixture.Provider.CreateAsyncScope();
            var preview = await scope.ServiceProvider.GetRequiredService<CommitmentImpactService>()
                .PreviewAsync(new PreviewCommitmentImpact(fixture.Tenant, fixture.Program, fixture.DraftId, 1),
                    CancellationToken.None);
            Assert.True(preview.IsSuccess, preview.Error?.Message);
            digest = preview.Value.Digest;
        }
        IRequest request = approval
            ? new ApproveCommitmentDraft(commandId, fixture.Tenant, fixture.Program, fixture.DraftId,
                1, fixture.ReviewId, new DateOnly(2026, 9, 1), "Approved", digest)
            : new ReviewCommitmentDraft(commandId, fixture.Tenant, fixture.Program, fixture.DraftId,
                1, "accept", "Reviewed", "Security owner", "applicable", "supported",
                SourceVerifiedReference: "MSA 4.1", SourceEvidence: "Signed MSA section 4.1");
        var user = approval ? fixture.Approver : fixture.Reviewer;
        await HttpAsync(fixture.Provider, user, request);
        var before = await fixture.ReadAsync();
        IRequest changed = request switch
        {
            ApproveCommitmentDraft command => command with { ExpectedRevision = 2 },
            ReviewCommitmentDraft command => command with { Rationale = "Changed" },
            _ => throw new InvalidOperationException(),
        };

        // Act
        await HttpAsync(fixture.Provider, user, request);
        await HttpAsync(fixture.Provider, user, changed, RequestErrorKind.Conflict);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(before.SourcePhysicalPosition, after.SourcePhysicalPosition);
        Assert.Equal(before.EffectiveVersionCount, after.EffectiveVersionCount);
    }

    [Fact]
    public async Task ShouldRetainAttributedCommitmentRetirementGivenNativeHttp()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(approval: true);
        var approval = await fixture.DecideAsync(approval: true, "http");
        Assert.True(approval.IsSuccess, approval.Error?.Message);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.RetireAsync("http");
        var after = await fixture.ReadAsync();
        var events = await fixture.ReadEventsAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(before.SourcePhysicalPosition + 1, after.SourcePhysicalPosition);
        Assert.True(after.IsRetired);
        Assert.Equal(new DateOnly(2027, 1, 1), after.RetiredEffectiveOn);
        var retired = Assert.IsType<CommitmentRetired>(events[^1]);
        Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.Approver).ToString(), retired.Actor.Id);
        Assert.Equal("The service commitment was superseded by the current engagement terms.", Assert.IsType<CommitmentRetiredContent>(Assert.Single((await fixture.ReadChangeAsync(retired)).Changes)).Rationale);
    }

    internal static async Task HttpAsync(IServiceProvider provider, Uuid user, IRequest request,
        RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            Context(user, "http"), CancellationToken.None);
        if (expectedFailure is { } error)
            Assert.Equal(error, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
    }

    static RequestDispatchContext Context(Uuid user, string transport) => new(ProgramManagementServices.Actor(user),
        transport == "http" ? new HttpInvocation("POST", "/synthetic/commitment/decision", "/synthetic/commitment/decision", "synthetic") :
        transport == "mcp" ? new McpInvocation("synthetic.commitment.decision") : new DirectInvocation());

    sealed class Fixture : IAsyncDisposable
    {
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid DraftId => CommitmentDraft.IdFor(Tenant, Program, "service_commitment", "SC-TRANSPORT");
        public Uuid Author { get; } = Uuid.CreateVersion4();
        public Uuid Reviewer { get; } = Uuid.CreateVersion4();
        public Uuid Approver { get; } = Uuid.CreateVersion4();
        public Uuid Other { get; } = Uuid.CreateVersion4();
        public Uuid ReviewId { get; } = Uuid.CreateVersion4();
        readonly ServiceProvider _provider;
        public ServiceProvider Provider => _provider;
        public FixtureClock Clock { get; } = new();
        readonly FitzCommitmentDraftDirectory _directory;
        readonly AsyncServiceScope _directoryScope;
        static readonly DateTimeOffset At = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        Fixture()
        {
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build(), developerAuthentication: true);
            var events = new InMemoryEventStore();
            var client = new InMemoryKvClient();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddSingleton<IKvClient>(client);
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
                new RecordingPermissionAuthorizer(allowed: true)));
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
            services.AddSingleton<TimeProvider>(Clock);
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _directoryScope = _provider.CreateAsyncScope();
            _directory = _directoryScope.ServiceProvider.GetRequiredService<FitzCommitmentDraftDirectory>();
        }

        public static async Task<Fixture> CreateAsync(bool approval, bool assigned = false)
        {
            var fixture = new Fixture();
            await ProgramManagementServices.SeedAsync(fixture._provider, new ProgramRules(fixture.Tenant, fixture.Program), program =>
            {
                Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                    fixture.Author, "Client manager", At));
                return Result.Success;
            });
            await ProgramManagementServices.SeedAsync(fixture._provider, new CommitmentRules(fixture.Tenant, fixture.DraftId), draft =>
            {
                Assert.True(draft.Create(fixture.Program, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                    "service_commitment", "SC-TRANSPORT", "Protect customer data", "Contractual commitment", "MSA 4.1",
                    fixture.Member(fixture.Author), "Author", At).IsSuccess);
                if (assigned)
                    Assert.Null(draft.AssignResponsibility(new ResponsibilityScope("commitment", draft.Id, draft.Id, draft.Revision),
                        Uuid.CreateVersion4(), fixture.Member(approval ? fixture.Approver : fixture.Reviewer),
                        approval ? ResponsibilityType.PolicyApprover : ResponsibilityType.AssignedReviewer,
                        fixture.Member(fixture.Author), "Author", At, At, null, []));
                if (approval)
                    Assert.Null(draft.Review(fixture.Program, draft.Revision, fixture.ReviewId, "accept", "Security owner",
                        "applicable", "supported", null, "Verified source", fixture.Member(fixture.Reviewer), "Reviewer",
                        At.AddMinutes(1), sourceVerifiedReference: "MSA 4.1", sourceEvidence: "Signed MSA section 4.1"));
                return Result.Success;
            });
            var pattern = EventStreamPattern.ForPattern(fixture.Tenant.ToString(), "commitment-drafts");
            await using var batch = await fixture._directory.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity(FitzCommitmentDraftDirectory.ProjectorName, pattern), ProjectionCheckpoint.Start));
            var cursor = EventCursor.Start;
            await foreach (var record in fixture._provider.GetRequiredService<IDomainEventReader>().ReadAsync(pattern, cursor, CancellationToken.None))
            {
                await fixture._directory.ApplyAsync(record.Event);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
            return fixture;
        }

        Uuid Member(Uuid user) => RbacIds.Member(Tenant, user);
        public Task<CommitmentRules> ReadAsync() => ProgramManagementServices.HydrateAsync(_provider, new CommitmentRules(Tenant, DraftId));
        public async Task<CommitmentMutationContent> ReadChangeAsync(ICommitmentFact fact)
        {
            await using var scope = _provider.CreateAsyncScope();
            return await CommitmentFactContent.ReadChangeAsync(scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), fact);
        }
        public async Task<IReadOnlyList<DomainEvent>> ReadEventsAsync()
        {
            var events = new List<DomainEvent>();
            await foreach (var record in _provider.GetRequiredService<IDomainEventReader>().ReadAsync(
                EventStreamPattern.ForPattern(Tenant.ToString(), "commitment-drafts"), EventCursor.Start, CancellationToken.None))
                events.Add(record.Event);
            return events;
        }
        public async Task<Result> DecideAsync(bool approval, string transport, Uuid? user = null)
        {
            var draft = await ReadAsync();
            await using var scope = _provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            IRequest request;
            if (approval)
            {
                var impact = await services.GetRequiredService<CommitmentImpactService>().PreviewAsync(
                    new PreviewCommitmentImpact(Tenant, Program, DraftId, draft.Revision), CancellationToken.None);
                Assert.True(impact.IsSuccess, impact.Error?.Message);
                Assert.True(impact.Value.Complete);
                request = new ApproveCommitmentDraft(Uuid.CreateVersion4(), Tenant, Program, DraftId, draft.Revision, ReviewId,
                    new DateOnly(2026, 9, 1), "Approved independently", impact.Value.Digest);
            }
            else
                request = new ReviewCommitmentDraft(Uuid.CreateVersion4(), Tenant, Program, DraftId, draft.Revision, "accept", "Reviewed independently",
                    "Security owner", "applicable", "supported", SourceVerifiedReference: "MSA 4.1", SourceEvidence: "Signed MSA section 4.1");
            return await services.GetRequiredService<IRequestBus>().DispatchAsync(request,
                Context(user ?? (approval ? Approver : Reviewer), transport), CancellationToken.None);
        }

        public async Task<Result> RetireAsync(string transport)
        {
            var draft = await ReadAsync();
            await using var scope = _provider.CreateAsyncScope();
            var request = new RetireCommitment(Uuid.CreateVersion4(), Tenant, Program, DraftId, draft.Revision,
                new DateOnly(2027, 1, 1),
                "The service commitment was superseded by the current engagement terms.");
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
                Context(Approver, transport), CancellationToken.None);
        }
        public sealed class FixtureClock : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = new(2026, 9, 1, 13, 0, 0, TimeSpan.Zero);
            public override DateTimeOffset GetUtcNow() => Now;
        }

        public async ValueTask DisposeAsync()
        {
            await _directoryScope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
