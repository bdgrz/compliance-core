using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class PersonalAccessReviewTransportTests
{
    public static TheoryData<string, string> NonHttpCases => new()
    {
        { "population", "direct" }, { "population", "mcp" },
        { "expectation", "direct" }, { "expectation", "mcp" },
        { "expectation_exception", "direct" }, { "expectation_exception", "mcp" },
        { "population_exception", "direct" }, { "population_exception", "mcp" },
        { "decision", "direct" }, { "decision", "mcp" },
        { "remediation_exception", "direct" }, { "remediation_exception", "mcp" },
        { "completion", "direct" }, { "completion", "mcp" }
    };

    [Theory]
    [MemberData(nameof(NonHttpCases))]
    public async Task ShouldPreserveSourceAndSnapshotGivenNonHttpPersonalAccessReviewDecision(string action, string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.SourcePositionAsync();
        var context = fixture.Context(transport);
        var snapshotBefore = await fixture.SnapshotAsync(context.RequestId);

        // Act
        var result = await fixture.DecideAsync(context);
        var snapshotAfter = await fixture.SnapshotAsync(context.RequestId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(RequestErrorKind.Forbidden, result.Kind);
        Assert.Contains("personal HTTP", result.Message!, StringComparison.Ordinal);
        Assert.Equal(before, await fixture.SourcePositionAsync());
        Assert.Equal(snapshotBefore.CommittedStreamPosition, snapshotAfter.CommittedStreamPosition);
        Assert.False(snapshotAfter.HasIntactContent);
    }

    [Theory]
    [InlineData("population")]
    [InlineData("expectation")]
    [InlineData("expectation_exception")]
    [InlineData("population_exception")]
    [InlineData("decision")]
    [InlineData("remediation_exception")]
    [InlineData("completion")]
    public async Task ShouldRetainAttributedAccessReviewDecisionGivenNativeHttp(string action)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.SourcePositionAsync();
        var context = fixture.Context("http");

        // Act
        var result = await fixture.DecideAsync(context);

        // Assert
        Assert.True(result.Success, result.Message);
        Assert.Equal(before + 1, await fixture.SourcePositionAsync());
        Assert.Equal(RbacIds.Member(fixture.Source.TenantId, fixture.User).ToString(), result.Actor!.Id);
        var snapshot = await fixture.SnapshotAsync(action == "population" ? await fixture.AcceptedSnapshotIdAsync() : context.RequestId);
        Assert.Equal(action == "population", snapshot.HasIntactContent);
    }

    [Theory]
    [InlineData("expectation", "proposer", RequestErrorKind.Forbidden, "The member who proposed an expectation cannot approve it.")]
    [InlineData("remediation_exception", "reviewer", RequestErrorKind.Forbidden, "The item's reviewer cannot approve an exception to its remediation.")]
    [InlineData("decision", "self", RequestErrorKind.Forbidden, "A reviewer cannot decide their own access without an active exact-scope waiver.")]
    [InlineData("decision", "unassigned", RequestErrorKind.NotFound, "The review item was not found among the reviewer's assignments.")]
    public async Task ShouldPreserveAccessReviewBusinessRefusalGivenNativeHttp(string action, string refusal, RequestErrorKind kind, string message)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.SourcePositionAsync();
        if (refusal == "reviewer")
            fixture.Source.Permissions.Allow(fixture.Source.ReviewerUserId);
        var user = refusal is "proposer" or "unassigned" ? fixture.Source.ManagerUserId :
            refusal == "reviewer" ? fixture.Source.ReviewerUserId : fixture.User;
        var context = fixture.Context("http", user);

        // Act
        var result = await fixture.DecideAsync(context, refusal);

        // Assert
        Assert.Equal(kind, result.Kind);
        Assert.Equal(message, result.Message);
        Assert.Equal(before, await fixture.SourcePositionAsync());
        Assert.Equal(0UL, (await fixture.SnapshotAsync(context.RequestId)).CommittedStreamPosition);
    }

    internal static bool IsPersonalDecision(IRequestBase request) => request is AcceptAccessPopulation or
        ApproveAccessExpectation or ExemptAccessExpectation or ExemptMissingAccessPopulation or
        RecordAccessDecision or RecordBulkAccessDecision or ExemptAccessRemediation or CompleteAccessReviewCampaign;

    internal static async Task<Result<T>> SendHttpAsync<T>(IServiceProvider provider, Uuid user, IRequest<T> request, RequestMetadata? metadata = null)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(user),
                new HttpInvocation("POST", "/synthetic/access-review/decision", "/synthetic/access-review/decision", "synthetic"), metadata), CancellationToken.None);
    }

    internal static Cntryl.Portia.RequestContext<T> HttpContext<T>(T request, ClaimsPrincipal actor) =>
        new(request, new RequestDispatchContext(actor,
            new HttpInvocation("POST", "/synthetic/access-review/decision", "/synthetic/access-review/decision", "synthetic")));

    sealed class Fixture(AccessReviewFixture source, ServiceProvider provider, string action) : IAsyncDisposable
    {
        public AccessReviewFixture Source { get; } = source;
        readonly ServiceProvider _provider = provider;
        readonly string _action = action;
        Uuid _sourceId;
        Uuid _itemId;
        Uuid _selfItemId;
        Uuid _expectationId;
        long _revision;
        public Uuid User { get; private set; }

        public static async Task<Fixture> CreateAsync(string action)
        {
            var source = await AccessReviewFixture.CreateAsync();
            var events = source.Provider.GetRequiredService<IEventStore>();
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build(), developerAuthentication: true);
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>((IDomainEventReader)events);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton(source.Provider.GetRequiredService<IImmutableContentStore>());
            services.AddSingleton<IPermissionAuthorizer>(source.Permissions);
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(
                new PermissionBackedAccessGrantPermissionAuthorizer(source.Permissions));
            services.AddSingleton<IAccessReviewSources>(source.Sources);
            services.AddSingleton<IApplicationDirectoryReader>(source.Applications);
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
            var fixture = new Fixture(source, services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }), action)
            { User = source.ManagerUserId, _sourceId = source.InstanceId };
            if (action == "population")
            {
                var opened = await source.SendAsync(source.ManagerUserId, new OpenAccessPopulation(Uuid.CreateVersion4(),
                    source.TenantId, source.ApplicationId, source.InstanceId, 1, AccessReviewFixture.Observed, "Observed export"));
                var facts = AccessReviewFixture.StandardFacts();
                var recorded = await source.SendAsync(source.ManagerUserId, new RecordAccessPopulationFacts(Uuid.CreateVersion4(),
                    source.TenantId, opened.PopulationId, 1, facts.Principals, facts.Entitlements, facts.GroupMembers, facts.Assignments));
                fixture._sourceId = opened.PopulationId;
                fixture._revision = recorded.Revision;
            }
            else if (action is "expectation" or "expectation_exception")
            {
                var proposed = await source.SendAsync(source.ManagerUserId, new ProposeAccessExpectation(Uuid.CreateVersion4(),
                    source.TenantId, source.ApplicationId, source.InstanceId, 0, "forbidden_principal_kind", new AccessExpectationParameters(PrincipalKind: "user_account"),
                    "Governed expectation", AccessReviewFixture.Observed.AddDays(-1)));
                fixture._expectationId = proposed.ExpectationId;
                fixture._revision = 1;
                fixture.User = source.ApproverUserId;
                if (action == "expectation_exception")
                {
                    await source.SendAsync(source.ApproverUserId, new ApproveAccessExpectation(Uuid.CreateVersion4(),
                        source.TenantId, source.InstanceId, proposed.ExpectationId, 1));
                    fixture._revision = 2;
                }
            }
            else if (action != "population_exception")
            {
                var (population, _) = await source.AcceptAsync(AccessReviewFixture.StandardFacts());
                await source.ClassifyStandardAsync(population);
                var launched = await source.LaunchAsync(population, action == "completion" ? source.ManagerMemberId : null,
                    action == "completion" ? "Named delegate for source preparation" : null,
                    action == "completion" ? source.ApproverMemberId : null);
                fixture._sourceId = launched.CampaignId;
                var campaign = await source.CampaignAsync(launched.CampaignId);
                fixture._itemId = AccessReviewCampaignTests.ItemId(campaign, "ada", "deploy");
                fixture._selfItemId = AccessReviewCampaignTests.ItemId(campaign, "rae", "admin");
                fixture._revision = campaign.Revision;
                fixture.User = source.ReviewerUserId;
                if (action == "remediation_exception")
                {
                    await source.SendAsync(source.ReviewerUserId, source.Decision(launched.CampaignId,
                        fixture._itemId, fixture._revision++, "modify", "Reduce access"));
                    fixture.User = source.ApproverUserId;
                }
                if (action == "completion")
                {
                    foreach (var item in campaign.Items)
                        await source.SendAsync(source.ManagerUserId, source.Decision(launched.CampaignId,
                            item.Item.ItemId, 1, "keep", "All access reviewed"));
                    fixture.User = source.ManagerUserId;
                }
            }
            return fixture;
        }

        public RequestDispatchContext Context(string transport, Uuid? user = null) => new(ProgramManagementServices.Actor(user ?? User),
            transport == "http" ? new HttpInvocation("POST", "/synthetic/access-review/decision", "/synthetic/access-review/decision", "synthetic") :
            transport == "mcp" ? new McpInvocation("synthetic.access-review.decision") : new DirectInvocation());

        public async Task<(bool Success, RequestErrorKind? Kind, string? Message, ActorReference? Actor)> DecideAsync(RequestDispatchContext context, string? refusal = null)
        {
            var expires = DateTimeOffset.UtcNow.AddDays(30);
            return _action switch
            {
                "population" => await SendAsync(new AcceptAccessPopulation(Uuid.CreateVersion4(), Source.TenantId, _sourceId, _revision, "Observed population attestation"), context),
                "expectation" => await SendAsync(new ApproveAccessExpectation(Uuid.CreateVersion4(), Source.TenantId, Source.InstanceId, _expectationId, _revision), context),
                "expectation_exception" => await SendAsync(new ExemptAccessExpectation(Uuid.CreateVersion4(), Source.TenantId, Source.InstanceId, _expectationId, _revision, "ada", "Time-bound exception", expires), context),
                "population_exception" => await SendAsync(new ExemptMissingAccessPopulation(_sourceId, Source.TenantId, Source.ApplicationId, Source.InstanceId, 0, "Population collection pending", expires), context),
                "decision" => await SendAsync(Source.Decision(_sourceId, refusal == "self" ? _selfItemId : _itemId, _revision, "keep", "Reviewed"), context),
                "remediation_exception" => await SendAsync(Source.Exception(_sourceId, _itemId, _revision, "Provider remediation pending", expires), context),
                _ => await SendAsync(Source.Completion(_sourceId, _revision, "Complete review attestation"), context)
            };
        }

        async Task<(bool Success, RequestErrorKind? Kind, string? Message, ActorReference? Actor)> SendAsync<T>(IRequest<T> request, RequestDispatchContext context)
        {
            await using var scope = _provider.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request, context, CancellationToken.None);
            var actor = (result.IsSuccess ? result.Value : default) switch
            {
                AccessExpectationView view => view.ApprovedBy,
                AccessExpectationExceptionView view => view.ApprovedBy,
                AccessPopulationExceptionView view => view.ApprovedBy,
                AccessDecisionView view => view.DecidedBy,
                AccessRemediationExceptionView view => view.ApprovedBy,
                OperationAcceptedResponse accepted => (await ProgramManagementServices.HydrateAsync(_provider,
                    new AccessReviewCampaign(Source.TenantId, _sourceId, selectedRootId: accepted.OperationId))).AcceptedFor(accepted.OperationId)!.Operation.Actor,
                AccessPopulationAcceptance => (await ProgramManagementServices.HydrateAsync(_provider,
                    new AccessPopulation(Source.TenantId, _sourceId))).AcceptedFact!.AcceptedBy,
                _ => null
            };
            return (result.IsSuccess, result.Error?.Kind, result.Error?.Message, actor);
        }

        public async Task<Uuid> AcceptedSnapshotIdAsync() => (await ProgramManagementServices.HydrateAsync(_provider,
            new AccessPopulation(Source.TenantId, _sourceId))).AcceptedFact!.SnapshotId;

        public Task<PopulationSnapshot> SnapshotAsync(Uuid id) => ProgramManagementServices.HydrateAsync(_provider, new PopulationSnapshot(Source.TenantId, id));
        public async Task<ulong> SourcePositionAsync() => _action switch
        {
            "population" => (await ProgramManagementServices.HydrateAsync(_provider, new AccessPopulation(Source.TenantId, _sourceId))).CommittedStreamPosition,
            "expectation" or "expectation_exception" => (await ProgramManagementServices.HydrateAsync(_provider,
                new AccessExpectation(Source.TenantId, _expectationId))).CommittedStreamPosition,
            "population_exception" => (await ProgramManagementServices.HydrateAsync(_provider,
                new AccessPopulationException(Source.TenantId, _sourceId))).CommittedStreamPosition,
            "decision" or "remediation_exception" => (await ProgramManagementServices.HydrateAsync(_provider,
                new AccessReviewItem(Source.TenantId, _sourceId, _itemId))).CommittedStreamPosition,
            _ => (await ProgramManagementServices.HydrateAsync(_provider, new AccessReviewCampaign(Source.TenantId, _sourceId))).CommittedStreamPosition
        };
        public async ValueTask DisposeAsync() { await _provider.DisposeAsync(); await Source.DisposeAsync(); }
    }
}
