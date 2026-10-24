using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Policies;

public sealed class PersonalPolicyDecisionTransportTests
{
    [Fact]
    public async Task ShouldReturnOriginalPeriodicReviewGivenExactHandlerRetry()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("periodic_review");
        var source = await fixture.ReadAsync();
        var commandId = Uuid.CreateVersion4();
        var request = new ConfirmPolicyReview(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.PolicyId,
            source.CurrentVersion!.Version, "The approved policy remains current.");
        var first = await IdentifiedHttpAsync(fixture.Provider, fixture.Reviewer, request, commandId);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var accepted = await fixture.ReadAsync();

        // Act
        var replay = await IdentifiedHttpAsync(fixture.Provider, fixture.Reviewer, request, commandId);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(accepted.SourcePhysicalPosition, retained.SourcePhysicalPosition);
        Assert.Equal(accepted.ReadDecisions().Count, retained.ReadDecisions().Count);
    }

    [Fact]
    public async Task ShouldReturnOriginalRevisionRegistrationGivenHandlerRetryAfterLaterEdit()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("review");
        var source = await fixture.ReadAsync();
        var commandId = Uuid.CreateVersion4();
        var request = new RevisePolicyDraft(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.PolicyId, 1,
            source.DraftContent! with { Body = "First revised policy body." });
        var first = await IdentifiedHttpAsync(fixture.Provider, fixture.Author, request, commandId);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var later = await IdentifiedHttpAsync(fixture.Provider, fixture.Author,
            new RevisePolicyDraft(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.PolicyId, 2,
                request.Content with { Body = "A later independent edit." }), Uuid.CreateVersion4());
        Assert.True(later.IsSuccess, later.Error?.Message);
        var advanced = await fixture.ReadAsync();

        // Act
        var replay = await IdentifiedHttpAsync(fixture.Provider, fixture.Author, request, commandId);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(2, replay.Value.Revision);
        Assert.Equal(3, retained.Revision);
        Assert.Equal("A later independent edit.", retained.DraftContent!.Body);
        Assert.Equal(advanced.SourcePhysicalPosition, retained.SourcePhysicalPosition);
    }

    [Fact]
    public async Task ShouldAcceptLegalEscapedBodyGivenCreatePolicyHandler()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("review");
        var body = new string('<', Policy.MaximumBodyLength);
        var request = new CreatePolicyDraft(Policy.IdFor(fixture.Tenant, fixture.Program, "POL-ESCAPED"), fixture.Tenant, fixture.Program, "POL-ESCAPED",
            new PolicyContent("Escaped body", "Preserve complete authored material", PolicyAudience.CoreSecurity,
                null, 12, body, null, "Security owner", []));

        // Act
        var result = await SendHttpAsync(fixture.Provider, fixture.Author, request);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var retained = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new PolicyRules(fixture.Tenant, result.Value.PolicyId));
        Assert.Equal(body, retained.DraftContent!.Body);
        Assert.Equal(1, retained.Revision);
    }

    static async Task<Result<T>> IdentifiedHttpAsync<T>(IServiceProvider provider, Uuid user,
        IRequest<T> request, Uuid commandId)
    {
        request = request switch
        {
            ConfirmPolicyReview confirm => (IRequest<T>)(object)(confirm with { CommandId = commandId }),
            RevisePolicyDraft revise => (IRequest<T>)(object)(revise with { CommandId = commandId }),
            _ => request,
        };
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(user),
                new HttpInvocation("POST", "/synthetic/policy/decision", "/synthetic/policy/decision", "synthetic"),
                new RequestMetadata(commandId, commandId)), CancellationToken.None);
    }

    [Fact]
    public async Task ShouldReturnFirstApprovalBodyGivenExactRetryAfterPolicyRetirement()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("approval");
        var source = await fixture.ReadAsync();
        var request = new ApprovePolicy(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program,
            fixture.PolicyId, 1, source.ReadDecisions()[0].DecisionId, new DateOnly(2026, 9, 1), true, "Approved");
        var first = await SendHttpAsync(fixture.Provider, fixture.Approver, request);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new PolicyRules(fixture.Tenant, fixture.PolicyId), policy =>
        {
            var author = RbacIds.Member(fixture.Tenant, fixture.Author);
            var reviewer = RbacIds.Member(fixture.Tenant, fixture.Reviewer);
            var approver = RbacIds.Member(fixture.Tenant, fixture.Approver);
            var now = DateTimeOffset.UtcNow;
            Assert.Null(policy.ProposeRetirement(fixture.Program, 1, new DateOnly(2027, 9, 1), "Retire",
                ActorReference.ForMember(author, "Author"), author, now));
            var reviewId = Uuid.CreateVersion4();
            Assert.Null(policy.Review(fixture.Program, policy.Revision, reviewId, "accept", "Reviewed retirement",
                ActorReference.ForMember(reviewer, "Reviewer"), reviewer, now));
            Assert.Null(policy.ApproveRetirement(fixture.Program, policy.Revision, Uuid.CreateVersion4(), reviewId,
                "Retired", ActorReference.ForMember(approver, "Approver"), approver, now));
            return Result.Success;
        });
        var before = await fixture.ReadAsync();

        // Act
        var replay = await SendHttpAsync(fixture.Provider, fixture.Approver, request);
        await SendHttpAsync(fixture.Provider, fixture.Approver, request with { ExpectedRevision = 2 },
            RequestErrorKind.Conflict);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(first.Value),
            System.Text.Json.JsonSerializer.Serialize(replay.Value));
        Assert.Equal("approved", replay.Value.Status);
        Assert.True(after.IsRetired);
        Assert.Equal(before.SourcePhysicalPosition, after.SourcePhysicalPosition);
    }

    [Fact]
    public async Task ShouldReturnOriginalReviewGivenSameCommandAfterLaterDraftRevision()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("review");
        var request = new ReviewPolicyDraft(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program,
            fixture.PolicyId, 1, "accept", "Reviewed");
        var first = await SendHttpAsync(fixture.Provider, fixture.Reviewer, request);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new PolicyRules(fixture.Tenant, fixture.PolicyId), policy =>
        {
            Assert.Null(policy.Revise(fixture.Program, 1, policy.DraftContent! with { Body = "Later body" },
                ActorReference.ForMember(RbacIds.Member(fixture.Tenant, fixture.Author), "Renamed author"),
                RbacIds.Member(fixture.Tenant, fixture.Author), DateTimeOffset.UtcNow));
            return Result.Success;
        });
        var before = await fixture.ReadAsync();

        // Act
        var replay = await SendHttpAsync(fixture.Provider, fixture.Reviewer, request);
        await SendHttpAsync(fixture.Provider, fixture.Reviewer, request with { Rationale = "Changed" },
            RequestErrorKind.Conflict);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(before.SourcePhysicalPosition, after.SourcePhysicalPosition);
    }

    [Theory]
    [InlineData("review", "direct")]
    [InlineData("review", "mcp")]
    [InlineData("approval", "direct")]
    [InlineData("approval", "mcp")]
    [InlineData("retirement_review", "direct")]
    [InlineData("retirement_review", "mcp")]
    [InlineData("retirement", "direct")]
    [InlineData("retirement", "mcp")]
    [InlineData("periodic_review", "direct")]
    [InlineData("periodic_review", "mcp")]
    public async Task ShouldRefusePersonalPolicyDecisionGivenNonHttpInvocation(string action, string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(action, transport);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Equal(RequestErrorKind.Forbidden, result.Kind);
        Assert.Contains("personal HTTP", result.Message!, StringComparison.Ordinal);
        Assert.Equal(before.SourcePhysicalPosition, retained.SourcePhysicalPosition);
        Assert.Equal(before.Revision, retained.Revision);
        Assert.Equal(before.ReadDecisions(), retained.ReadDecisions());
    }

    [Theory]
    [InlineData("review")]
    [InlineData("approval")]
    [InlineData("retirement_review")]
    [InlineData("retirement")]
    [InlineData("periodic_review")]
    public async Task ShouldRetainAttributedPolicyDecisionGivenNativeHttp(string action)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(action, "http");
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.True(result.Success, result.Message);
        Assert.Equal(before.SourcePhysicalPosition + 1, retained.SourcePhysicalPosition);
        Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.Decider(action)).ToString(), retained.ReadDecisions()[^1].Actor.Id);
        Assert.Equal(before.ReadDecisions().Count + 1, retained.ReadDecisions().Count);
    }

    [Theory]
    [InlineData("review", "author", "A policy author cannot review a revision they proposed.")]
    [InlineData("retirement_review", "author", "A policy author cannot review a revision they proposed.")]
    [InlineData("approval", "author", "A policy approver must be independent of its authors and accepted reviewer.")]
    [InlineData("approval", "reviewer", "A policy approver must be independent of its authors and accepted reviewer.")]
    [InlineData("retirement", "author", "A retirement approver must be independent of its proposer and accepted reviewer.")]
    [InlineData("retirement", "reviewer", "A retirement approver must be independent of its proposer and accepted reviewer.")]
    public async Task ShouldPreservePolicySeparationGivenNativeHttpConflictingActor(string action, string actor, string message)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(action, "http", actor == "author" ? fixture.Author : fixture.Reviewer);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Kind);
        Assert.Equal(message, result.Message);
        Assert.Equal(before.SourcePhysicalPosition, retained.SourcePhysicalPosition);
    }

    internal static async Task<Result<T>> SendHttpAsync<T>(IServiceProvider provider, Uuid user,
        IRequest<T> request, RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(user),
                new HttpInvocation("POST", "/synthetic/policy/decision", "/synthetic/policy/decision", "synthetic")), CancellationToken.None);
        if (expectedFailure is { } kind)
            Assert.Equal(kind, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    sealed class Fixture : IAsyncDisposable
    {
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid PolicyId => Policy.IdFor(Tenant, Program, "POL-TRANSPORT");
        public Uuid Author { get; } = Uuid.CreateVersion4();
        public Uuid Reviewer { get; } = Uuid.CreateVersion4();
        public Uuid Approver { get; } = Uuid.CreateVersion4();
        readonly ServiceProvider _provider;
        public ServiceProvider Provider => _provider;
        Uuid _review;
        static readonly DateOnly Effective = new(2026, 9, 1);
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
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
                new RecordingPermissionAuthorizer(allowed: true)));
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        public static async Task<Fixture> CreateAsync(string action)
        {
            var fixture = new Fixture();
            await ProgramManagementServices.SeedAsync(fixture._provider, new ComplianceProgram(fixture.Tenant, fixture.Program), program =>
            {
                Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                    fixture.Author, "Client manager", At));
                return Result.Success;
            });
            await ProgramManagementServices.SeedAsync(fixture._provider, new PolicyRules(fixture.Tenant, fixture.PolicyId), policy =>
            {
                Assert.True(policy.Create(fixture.Program, Uuid.CreateVersion4(), "POL-TRANSPORT",
                    new PolicyContent("Access control", "Govern access", PolicyAudience.CoreSecurity, null, 12,
                        "Body", null, "Security owner", []), fixture.Actor(fixture.Author), fixture.Member(fixture.Author), At).IsSuccess);
                if (action != "review")
                {
                    fixture._review = Uuid.CreateVersion4();
                    Assert.Null(policy.Review(fixture.Program, policy.Revision, fixture._review, "accept", "Reviewed",
                        fixture.Actor(fixture.Reviewer), fixture.Member(fixture.Reviewer), At.AddMinutes(1)));
                }
                if (action is "retirement" or "retirement_review" or "periodic_review")
                    Assert.Null(policy.Approve(fixture.Program, policy.Revision, Uuid.CreateVersion4(), fixture._review,
                        Effective, true, "Approved", null, fixture.Actor(fixture.Approver), fixture.Member(fixture.Approver), At.AddMinutes(2)));
                if (action is "retirement" or "retirement_review")
                {
                    Assert.Null(policy.ProposeRetirement(fixture.Program, policy.CurrentVersion!.Version,
                        Effective.AddYears(1), "Retire", fixture.Actor(fixture.Author), fixture.Member(fixture.Author), At.AddMinutes(3)));
                    if (action == "retirement")
                    {
                        fixture._review = Uuid.CreateVersion4();
                        Assert.Null(policy.Review(fixture.Program, policy.Revision, fixture._review, "accept", "Reviewed retirement",
                            fixture.Actor(fixture.Reviewer), fixture.Member(fixture.Reviewer), At.AddMinutes(4)));
                    }
                }
                return Result.Success;
            });
            return fixture;
        }

        public Uuid Decider(string action) => action is "review" or "retirement_review" or "periodic_review" ? Reviewer : Approver;
        Uuid Member(Uuid user) => RbacIds.Member(Tenant, user);
        ActorReference Actor(Uuid user) => ActorReference.ForMember(Member(user), "Client member");
        public Task<PolicyRules> ReadAsync() => ProgramManagementServices.HydrateAsync(_provider, new PolicyRules(Tenant, PolicyId));
        public async Task<(bool Success, RequestErrorKind? Kind, string? Message)> DecideAsync(string action, string transport, Uuid? actor = null)
        {
            var policy = await ReadAsync();
            return action switch
            {
                "review" or "retirement_review" => await SendAsync(new ReviewPolicyDraft(Uuid.CreateVersion4(), Tenant, Program, PolicyId, policy.Revision, "accept", "Reviewed"), transport, actor ?? Decider(action)),
                "approval" => await SendAsync(new ApprovePolicy(Uuid.CreateVersion4(), Tenant, Program, PolicyId, policy.Revision, _review, Effective, true, "Approved"), transport, actor ?? Decider(action)),
                "retirement" => await SendAsync(new ApprovePolicyRetirement(Uuid.CreateVersion4(), Tenant, Program, PolicyId, policy.Revision, _review, "Retired"), transport, actor ?? Decider(action)),
                _ => await SendAsync(new ConfirmPolicyReview(Uuid.CreateVersion4(), Tenant, Program, PolicyId, policy.CurrentVersion!.Version, "Confirmed"), transport, actor ?? Decider(action))
            };
        }
        async Task<(bool Success, RequestErrorKind? Kind, string? Message)> SendAsync<T>(IRequest<T> request, string transport, Uuid actor)
        {
            await using var scope = _provider.CreateAsyncScope();
            RequestInvocation invocation = transport == "http" ? new HttpInvocation("POST", "/synthetic/policy/decision", "/synthetic/policy/decision", "synthetic") :
                transport == "mcp" ? new McpInvocation("synthetic.policy.decision") : new DirectInvocation();
            var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
                new RequestDispatchContext(ProgramManagementServices.Actor(actor), invocation), CancellationToken.None);
            return (result.IsSuccess, result.Error?.Kind, result.Error?.Message);
        }
        public ValueTask DisposeAsync() => _provider.DisposeAsync();
    }
}
