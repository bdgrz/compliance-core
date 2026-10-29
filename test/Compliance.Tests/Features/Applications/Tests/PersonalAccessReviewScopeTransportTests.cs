using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Fitz;
using Cntryl.Portia.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Workforce.Rules;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
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
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

public sealed class PersonalAccessReviewScopeTransportTests
{
    [Fact]
    public async Task ShouldKeepScopeAuthorityBoundedGivenManyEffectiveDecisionsWithFullHistory()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var effectiveFrom = DateTimeOffset.UtcNow;
        for (var count = 0; count < 32; count++)
        {
            var decision = await fixture.DecideAsync("http", count: count, effectiveFrom: effectiveFrom);
            Assert.True(decision.IsSuccess, decision.Error?.Message);
        }

        // Act
        var owner = await fixture.ReadOwnerAsync();
        var read = await fixture.ReadAsync();

        // Assert
        Assert.Equal(32, read.Decisions.Count);
        Assert.All(typeof(SystemInstanceAccessReviewScope).GetFields(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.DeclaredOnly), field =>
            Assert.False(field.GetValue(owner) is System.Collections.ICollection { Count: > 2 },
                $"The scope owner retains lifetime decisions in {field.Name}."));
    }

    [Fact]
    public async Task ShouldReplayOriginalScopeDecisionGivenExactHttpRetryAfterInstanceRevision()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var effectiveFrom = DateTimeOffset.UtcNow;
        var first = await fixture.DecideAsync("http", effectiveFrom: effectiveFrom);
        Assert.True(first.IsSuccess, first.Error?.Message);
        await fixture.RetireInstanceAsync();

        // Act
        var replay = await fixture.DecideAsync("http", effectiveFrom: effectiveFrom);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(1, replay.Value.SystemInstanceRevision);
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    [InlineData("http")]
    public async Task ShouldEnforcePersonalTransportGivenValidScopeApproval(string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var instanceBefore = await fixture.ReadInstanceAsync();

        // Act
        var result = await fixture.DecideAsync(transport);
        var after = await fixture.ReadAsync();
        var instanceAfter = await fixture.ReadInstanceAsync();

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(before.CommittedStreamPosition + 1, after.CommittedStreamPosition);
            var decision = Assert.Single(after.Decisions);
            Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.Approver).ToString(), decision.ApprovedBy.Id);
            Assert.Equal(fixture.Application, decision.ApplicationId);
            Assert.Equal(fixture.Instance, decision.SystemInstanceId);
            Assert.Equal(1, decision.SystemInstanceRevision);
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
            Assert.Empty(after.Decisions);
        }
        Assert.Equal(instanceBefore.CommittedStreamPosition, instanceAfter.CommittedStreamPosition);
        Assert.Equal(instanceBefore.Revision, instanceAfter.Revision);
    }

    [Theory]
    [InlineData("registrant")]
    [InlineData("owner")]
    public async Task ShouldPreserveScopeSeparationGivenNativeHttpConflictingActor(string role)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync("http", role == "registrant" ? fixture.Registrant : fixture.Owner);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("The registrant or access owner cannot approve the system instance's access-review scope without an active exact-scope waiver.", result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveOrganizationManagementGrantGivenNativeHttpWithoutGrant()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        fixture.Permissions.ManagesProgram = false;
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync("http");
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("Access-review scope approval requires organization-wide program management.", result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveHistoricalManagementWallGivenNativeHttpWithOrdinaryGrants()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAttestHistoryAsync();
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync("http");
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("Attest", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveApplicationParentGivenNativeHttpWithForeignApplication()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var accepted = await fixture.DecideAsync("http");
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        var other = await fixture.DeclareApplicationAsync();
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync("http", application: other);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal("The system instance was not found.", result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveExactInstanceRevisionGivenNativeHttpWithStaleSource()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync("http", revision: 2);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("The system instance is at revision 1; the decision names revision 2.", result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Registrant { get; } = Uuid.CreateVersion4();
        public Uuid Owner { get; } = Uuid.CreateVersion4();
        public Uuid Approver { get; } = Uuid.CreateVersion4();
        public Uuid Application { get; private set; }
        public Uuid Instance { get; private set; }
        public Permissions Permissions { get; } = new();

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
            services.AddSingleton<IPermissionAuthorizer>(Permissions);
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(Permissions));
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            var personId = Uuid.CreateVersion4();
            await ProgramManagementServices.SeedAsync(fixture._provider, new PersonRules(fixture.Tenant, personId), person =>
            {
                var actor = ActorReference.ForMember(RbacIds.Member(fixture.Tenant, fixture.Registrant), "Registrant");
                Assert.True(person.Record("Access owner", null, actor, DateTimeOffset.UtcNow).IsSuccess);
                Assert.Null(person.CorrelateMembership(1, fixture.Owner, actor, DateTimeOffset.UtcNow));
                return Result.Success;
            });
            fixture.Application = await fixture.DeclareApplicationAsync(personId);
            await using var scope = fixture._provider.CreateAsyncScope();
            var instance = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
                new DeclareSystemInstance(Uuid.CreateVersion4(), fixture.Tenant, fixture.Application, 1, "Production", "production"),
                ProgramManagementServices.Actor(fixture.Registrant));
            Assert.True(instance.IsSuccess, instance.Error?.Message);
            fixture.Instance = instance.Value.SystemInstanceId;
            return fixture;
        }
        public async Task<Uuid> DeclareApplicationAsync(Uuid? owner = null)
        {
            await using var scope = _provider.CreateAsyncScope();
            var application = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
                new DeclareApplication(Uuid.CreateVersion4(), Tenant, "Payroll", "Run payroll", AccessOwnerPersonId: owner),
                ProgramManagementServices.Actor(Registrant));
            Assert.True(application.IsSuccess, application.Error?.Message);
            return application.Value.ApplicationId;
        }
        public Task SeedAttestHistoryAsync() => AttestAssignmentHistoryFixture.SeedAsync(_provider, Tenant, Approver, revoked: true);
        public async Task<ScopeRead> ReadAsync()
        {
            await using var scope = _provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var reader = services.GetRequiredService<IAggregateReader>();
            var owner = await reader.HydrateAsync(new SystemInstanceAccessReviewScope(Tenant, Instance));
            var decisions = await AccessReviewScopeReader.ReadAsync(reader, services.GetRequiredService<IDomainEventReader>(),
                services.GetRequiredService<IImmutableContentStore>(), Tenant, Application, Instance);
            return new ScopeRead(owner.CommittedStreamPosition, decisions);
        }
        public Task<SystemInstanceAccessReviewScope> ReadOwnerAsync() => ProgramManagementServices.HydrateAsync(
            _provider, new SystemInstanceAccessReviewScope(Tenant, Instance));
        public Task<DeclaredSystemInstance> ReadInstanceAsync() => ProgramManagementServices.HydrateAsync(_provider, new DeclaredSystemInstance(Tenant, Instance));
        public async Task<Result<AccessReviewScopeDecisionView>> DecideAsync(string transport, Uuid? user = null, Uuid? application = null, long revision = 1, long count = 0, DateTimeOffset? effectiveFrom = null)
        {
            await using var scope = _provider.CreateAsyncScope();
            RequestInvocation invocation = transport == "http" ? new HttpInvocation("POST", "/synthetic/scope/decision", "/synthetic/scope/decision", "synthetic") :
                transport == "mcp" ? new McpInvocation("synthetic.scope.decision") : new DirectInvocation();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
                new DecideAccessReviewScope(Tenant, application ?? Application, Instance, revision, count, "included", "Production data", effectiveFrom ?? DateTimeOffset.UtcNow),
                new RequestDispatchContext(ProgramManagementServices.Actor(user ?? Approver), invocation), CancellationToken.None);
        }
        public async Task RetireInstanceAsync()
        {
            await ProgramManagementServices.SeedAsync(_provider, new DeclaredSystemInstance(Tenant, Instance), instance =>
            {
                Assert.Null(instance.Retire(1, DateTimeOffset.UtcNow, "Closed", RbacIds.Member(Tenant, Registrant),
                    "Registrant", DateTimeOffset.UtcNow));
                return Result.Success;
            });
        }

        public ValueTask DisposeAsync() => _provider.DisposeAsync();
    }

    sealed record ScopeRead(ulong CommittedStreamPosition, IReadOnlyList<AccessReviewScopeDecisionView> Decisions);

    sealed class Permissions : IPermissionAuthorizer
    {
        public bool ManagesProgram { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(permission != IProgramScopedRequest.ManagementPermission || ManagesProgram);
    }
}
