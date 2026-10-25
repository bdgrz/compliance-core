using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution;

public sealed class PersonalCampaignWaiverTransportTests
{
    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    [InlineData("http")]
    public async Task ShouldRequirePersonalHttpGivenValidFrozenCampaignException(string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.SendAsync(fixture.Request(), transport);
        var retained = await fixture.ReadAsync();

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(before.CommittedStreamPosition + 1, retained.CommittedStreamPosition);
            var applied = await fixture.ApplyAsync(result.Value.OperationId);
            Assert.True(applied.IsSuccess, applied.Error?.Message);
            var waiver = Assert.Single((await fixture.ParticipantAsync()).Waivers);
            Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.Manager).ToString(), waiver.Approver.Id);
            Assert.Equal(fixture.PersonId, waiver.PersonId);
            var state = await fixture.StateAsync();
            Assert.Equal(0, state.Totals(fixture.Today).Satisfied);
            Assert.Equal(1, state.Totals(fixture.Today).Excepted);
        }
        else
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
            Assert.Empty((await fixture.ParticipantAsync()).Waivers);
        }
        Assert.Equal(before.Subject, retained.Subject);
        Assert.Equal(before.Launch!.RosterSnapshotId, retained.Launch!.RosterSnapshotId);
    }

    [Theory]
    [InlineData("grant", RequestErrorKind.Forbidden, "Campaign exceptions require organization-wide program management.")]
    [InlineData("self", RequestErrorKind.Forbidden, "A member cannot approve their own exception.")]
    [InlineData("outside", RequestErrorKind.NotFound, "The person is not in this campaign's current audience.")]
    [InlineData("expiry", RequestErrorKind.Validation, "An exception must expire within 12 months.")]
    [InlineData("program", RequestErrorKind.NotFound, "The program was not found.")]
    [InlineData("closed", RequestErrorKind.Conflict, "The campaign is closed.")]
    [InlineData("history", RequestErrorKind.Forbidden, "A person with actual Attest assignment history cannot author or approve this client's management records.")]
    public async Task ShouldPreserveCampaignExceptionAuthorityGivenNativeHttpRefusal(string refusal, RequestErrorKind kind, string message)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(refusal);
        var before = await fixture.ReadAsync();
        var request = fixture.Request();
        if (refusal == "outside")
            request = request with { PersonId = Uuid.CreateVersion4() };
        if (refusal == "expiry")
            request = request with { ExpiresOn = fixture.Today.AddMonths(12).AddDays(1) };
        if (refusal == "program")
            request = request with { ProgramId = Uuid.CreateVersion4() };

        // Act
        var result = await fixture.SendAsync(request, "http");
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.Equal(kind, result.Error?.Kind);
        Assert.Equal(message, result.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
        Assert.Empty((await fixture.ParticipantAsync()).Waivers);
    }

    [Fact]
    public async Task ShouldKeepOneExceptionGivenExactNativeHttpRetry()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.Request();
        var metadata = new RequestMetadata(request.CommandId, request.CommandId, null);

        // Act
        var first = await fixture.SendAsync(request, "http", metadata);
        var before = await fixture.ReadAsync();
        var retry = await fixture.SendAsync(request, "http", metadata);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
    }

    sealed class Permissions : IAccessGrantPermissionAuthorizer
    {
        public bool OrganizationWide { get; set; } = true;
        public Uuid Program { get; set; }
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, Uuid programId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(OrganizationWide || programId == Program);
        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, string permission, CancellationToken ct = default) => ValueTask.FromResult(
                new ProgramAccessVisibility(OrganizationWide, new HashSet<Uuid> { Program }));
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly Permissions _permissions = new();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Manager { get; } = Uuid.CreateVersion4();
        public Uuid PersonId { get; } = Uuid.CreateVersion4();
        Uuid _campaign;
        readonly DateTimeOffset _at = DateTimeOffset.UtcNow;
        public DateOnly Today => DateOnly.FromDateTime(_at.UtcDateTime);

        Fixture()
        {
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            }).Build(), developerAuthentication: true);
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(_permissions);
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        public static async Task<Fixture> CreateAsync(string? refusal = null)
        {
            var fixture = new Fixture();
            var actor = ActorReference.ForMember(RbacIds.Member(fixture.Tenant, fixture.Manager), "Client manager");
            await ProgramManagementServices.SeedAsync(fixture._provider, new ComplianceProgram(fixture.Tenant, fixture.Program), program =>
            {
                Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null), fixture.Manager, "Client manager", fixture._at));
                return Result.Success;
            });
            await ProgramManagementServices.SeedAsync(fixture._provider, new Person(fixture.Tenant, fixture.PersonId), person =>
            {
                Assert.True(person.Record("Worker", null, actor, fixture._at).IsSuccess);
                if (refusal == "self")
                    Assert.Null(person.CorrelateMembership(1, fixture.Manager, actor, fixture._at));
                return Result.Success;
            });
            var policyId = Policy.IdFor(fixture.Tenant, fixture.Program, "POL-WAIVER");
            await ProgramManagementServices.SeedAsync(fixture._provider, new PolicyRules(fixture.Tenant, policyId), policy =>
            {
                var author = Uuid.CreateVersion4();
                var reviewer = Uuid.CreateVersion4();
                Assert.True(policy.Create(fixture.Program, Uuid.CreateVersion4(), "POL-WAIVER", new PolicyContent("Security", "Govern", PolicyAudience.CoreSecurity, null, 12, "Body", null, "Owner", []),
                    ActorReference.ForMember(author, "Author"), author, fixture._at).IsSuccess);
                var reviewId = Uuid.CreateVersion4();
                Assert.Null(policy.Review(fixture.Program, policy.Revision, reviewId, "accept", "Reviewed", ActorReference.ForMember(reviewer, "Reviewer"), reviewer, fixture._at));
                Assert.Null(policy.Approve(fixture.Program, policy.Revision, Uuid.CreateVersion4(), reviewId, fixture.Today, true, "Approved", null, actor, RbacIds.Member(fixture.Tenant, fixture.Manager), fixture._at));
                return Result.Success;
            });
            var rosterId = Uuid.CreateVersion4();
            var rows = WorkforceRosterSnapshotContent.Rows([new PersonView(fixture.Tenant, fixture.PersonId, 1, "Worker", null, "manual", actor, fixture._at)],
                [new WorkRelationshipView(fixture.Tenant, WorkRelationship.IdFor(fixture.Tenant, "E-1"), 1, fixture.PersonId, "E-1", "employee", "active", fixture.Today.AddYears(-1), null, "Engineering", null, null, false, "manual", actor, fixture._at)]);
            var hash = PopulationContentIdentity.Compute(WorkforceRosterSnapshotContent.Kind, rows).Value.Sha256;
            await ProgramManagementServices.SeedAsync(fixture._provider, new PopulationSnapshot(fixture.Tenant, rosterId), snapshot =>
                snapshot.Freeze(rosterId, null, WorkforceRosterSnapshotContent.Kind, rows, hash, null, actor, fixture._at));
            await using (var scope = fixture._provider.CreateAsyncScope())
            {
                var launched = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(new LaunchPolicyCampaign(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, policyId, 1, rosterId, fixture.Today.AddDays(30)),
                    new RequestDispatchContext(ProgramManagementServices.Actor(fixture.Manager), new DirectInvocation()), CancellationToken.None);
                Assert.True(launched.IsSuccess, launched.Error?.Message);
                fixture._campaign = launched.Value.CampaignId;
            }
            if (refusal == "closed")
                await CampaignSeed.CloseAsync(fixture._provider, fixture.Tenant, fixture._campaign, "Closed", actor,
                    fixture._at);
            if (refusal == "history")
                await AttestAssignmentHistoryFixture.SeedAsync(fixture._provider, fixture.Tenant, fixture.Manager, revoked: true);
            if (refusal == "grant")
            {
                // Keep scoped current management authorized, but remove the handler's organization-wide authority.
                fixture._permissions.OrganizationWide = false;
                fixture._permissions.Program = fixture.Program;
            }
            return fixture;
        }

        public ApproveCampaignWaiver Request() => new(Uuid.CreateVersion4(), Tenant, Program, _campaign, PersonId, "Extended leave", Today.AddMonths(3), 1);
        public Task<PolicyCampaignSnapshot> ReadAsync() => CampaignSeed.CampaignAsync(_provider, Tenant, _campaign);
        public Task<PolicyCampaignParticipantSnapshot> ParticipantAsync() => CampaignSeed.ParticipantAsync(_provider, Tenant, _campaign, PersonId);
        public Task<Result<ReceiverOutcome>> ApplyAsync(Uuid commandId) => CampaignSeed.ApplyAdmittedAsync(_provider, Tenant, _campaign, PersonId, commandId, CampaignParticipantResultKind.Waiver);
        public async Task<CampaignState> StateAsync()
        {
            await using var scope = _provider.CreateAsyncScope();
            return (await new CampaignSource(scope.ServiceProvider.GetRequiredService<IAggregateReader>(), scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), scope.ServiceProvider.GetRequiredService<IDomainEventReader>()).ReadAsync(Tenant, Program, _campaign, CancellationToken.None)).Value;
        }
        public async Task<Result<OperationAcceptedResponse>> SendAsync(ApproveCampaignWaiver request, string transport, RequestMetadata? metadata = null)
        {
            await using var scope = _provider.CreateAsyncScope();
            RequestInvocation invocation = transport == "http" ? new HttpInvocation("PUT", "/synthetic/campaign/waiver", "/synthetic/campaign/waiver", "synthetic") : transport == "mcp" ? new McpInvocation("synthetic.campaign.waiver") : new DirectInvocation();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
                new RequestDispatchContext(ProgramManagementServices.Actor(Manager), invocation, metadata), CancellationToken.None);
        }
        public ValueTask DisposeAsync() => _provider.DisposeAsync();
    }
}
