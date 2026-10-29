using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
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
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;

public sealed class PersonalMappingDecisionTransportTests
{
    [Theory]
    [InlineData("proposal", "direct")]
    [InlineData("proposal", "mcp")]
    [InlineData("applicability", "direct")]
    [InlineData("applicability", "mcp")]
    [InlineData("mapping", "direct")]
    [InlineData("mapping", "mcp")]
    [InlineData("proposal", "http")]
    [InlineData("applicability", "http")]
    [InlineData("mapping", "http")]
    public async Task ShouldEnforcePersonalTransportGivenValidMappingDecision(string action, string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.PositionAsync(action);

        // Act
        var result = await fixture.DecideAsync(action, transport);
        var after = await fixture.PositionAsync(action);

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(before + 1, after);
            var actor = await fixture.LastActorAsync(action);
            Assert.Equal(RbacIds.Member(fixture.Tenant, action == "proposal" ? fixture.Author : fixture.Reviewer).ToString(), actor.Id);
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before, after);
        }
    }

    [Theory]
    [InlineData("applicability", "A not-applicable proposer cannot review their own proposal.")]
    [InlineData("mapping", "A mapping proposer cannot review their own proposal.")]
    public async Task ShouldPreserveProposerSeparationGivenNativeHttpSelfReview(string action, string message)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.PositionAsync(action);

        // Act
        var result = await fixture.DecideAsync(action, "http", fixture.Author);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(message, result.Error!.Message);
        Assert.Equal(before, await fixture.PositionAsync(action));
    }

    [Theory]
    [InlineData("proposal", "A not-applicable decision must target the criteria edition the program has selected.")]
    [InlineData("applicability", "The decision targets an edition the program no longer selects.")]
    [InlineData("mapping", "The mapping targets an edition the program no longer selects; remap it explicitly.")]
    public async Task ShouldPreserveSelectedEditionGivenNativeHttpAfterProgramChange(string action, string message)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        await fixture.ChangeEditionAsync();
        var before = await fixture.PositionAsync(action);

        // Act
        var result = await fixture.DecideAsync(action, "http");

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(message, result.Error!.Message);
        Assert.Equal(before, await fixture.PositionAsync(action));
    }

    [Fact]
    public async Task ShouldPreserveCatalogueCriterionGivenNativeHttpPointOfFocusProposal()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("proposal");

        // Act
        var result = await fixture.DecideAsync("proposal", "http", criterion: "bdgrz:focus:cc6.1-access");

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal("Not-applicable decisions apply to a criterion of the selected edition, not a point of focus.", result.Error!.Message);
        Assert.Equal(0UL, await fixture.PositionAsync("proposal"));
    }

    [Theory]
    [InlineData("proposal")]
    [InlineData("applicability")]
    [InlineData("mapping")]
    public async Task ShouldPreserveSourceGrantGivenNativeHttpWithoutManagement(string action)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        fixture.Permissions.Allowed = false;
        var before = await fixture.PositionAsync(action);

        // Act
        var result = await fixture.DecideAsync(action, "http");

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("The actor may not manage this program.", result.Error!.Message);
        Assert.Equal(before, await fixture.PositionAsync(action));
    }

    [Theory]
    [InlineData("proposal")]
    [InlineData("applicability")]
    [InlineData("mapping")]
    public async Task ShouldPreserveOwningProgramGivenNativeHttpWithForeignProgram(string action)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.PositionAsync(action);

        // Act
        var result = await fixture.DecideAsync(action, "http", program: Uuid.CreateVersion4());

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal("The program was not found.", result.Error!.Message);
        Assert.Equal(before, await fixture.PositionAsync(action));
    }

    internal static async Task<Result> HttpAsync(IServiceProvider provider, Uuid user, IRequest request, RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request, Context(user, "http"), CancellationToken.None);
        if (expectedFailure is { } kind)
            Assert.Equal(kind, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }
    internal static async Task<Result<T>> HttpAsync<T>(IServiceProvider provider, Uuid user, IRequest<T> request, RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request, Context(user, "http"), CancellationToken.None);
        if (expectedFailure is { } kind)
            Assert.Equal(kind, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }
    internal static RequestDispatchContext Context(Uuid user, string transport) => new(ProgramManagementServices.Actor(user),
        transport == "http" ? new HttpInvocation("POST", "/synthetic/mapping/decision", "/synthetic/mapping/decision", "synthetic") :
        transport == "mcp" ? new McpInvocation("synthetic.mapping.decision") : new DirectInvocation());

    sealed class Permissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        static readonly Uuid Edition = Uuid.CreateVersion5(Uuid.Empty, "mapping-test-current");
        static readonly Uuid NextEdition = Uuid.CreateVersion5(Uuid.Empty, "mapping-test-next");
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Author { get; } = Uuid.CreateVersion4();
        public Uuid Reviewer { get; } = Uuid.CreateVersion4();
        Uuid _decision;
        public Permissions Permissions { get; } = new();

        readonly ServiceProvider _sourceProvider;

        Fixture(ControlCriterionMappingHandlerTests.Fixture source)
        {
            _sourceProvider = source.Provider;
            Tenant = source.TenantId;
            Program = source.ProgramId;
            Author = source.AuthorUserId;
            Reviewer = source.ReviewerUserId;
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build(), developerAuthentication: true);
            var events = source.Provider.GetRequiredService<IEventStore>();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>((IDomainEventReader)events);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton(source.Provider.GetRequiredService<IImmutableContentStore>());
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(Permissions));
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
            services.AddSingleton<ICriteriaCatalog>(ControlCriterionMappingHandlerTests.TestCatalog());
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }
        public static async Task<Fixture> CreateAsync(string action)
        {
            var source = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync();
            var fixture = new Fixture(source);
            await using var scope = fixture._provider.CreateAsyncScope();
            var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
            if (action == "mapping")
            {
                var proposed = await bus.DispatchAsync(new ProposeControlCriterionMapping(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program,
                    source.ControlId, source.ControlVersionId, Edition, "CC6.1", 0, "Access is reviewed", "Production scope"),
                    Context(fixture.Author, "direct"), CancellationToken.None);
                Assert.True(proposed.IsSuccess, proposed.Error?.Message);
                fixture._decision = proposed.Value.MappingId;
            }
            else if (action == "applicability")
            {
                var proposed = await bus.DispatchAsync(new ProposeCriterionNotApplicable(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program,
                    Edition, "CC6.2", 0, "Not applicable"), Context(fixture.Author, "http"), CancellationToken.None);
                Assert.True(proposed.IsSuccess, proposed.Error?.Message);
                fixture._decision = proposed.Value.DecisionId;
            }
            return fixture;
        }
        public Task ChangeEditionAsync() => ProgramManagementServices.SeedAsync(_provider, new ProgramRules(Tenant, Program),
            program => { Assert.Null(program.SelectCriteriaEdition(2, NextEdition, Author, "Author", DateTimeOffset.UtcNow)); return Result.Success; });
        public async Task<ulong> PositionAsync(string action)
        {
            var facts = 0UL;
            var pattern = EventStreamPattern.ForPattern(Tenant.ToString(), action == "mapping" ? "control-criterion-mappings" : "criterion-applicability");
            await foreach (var record in _provider.GetRequiredService<IDomainEventReader>().ReadAsync(pattern, EventCursor.Start, CancellationToken.None))
                if (!record.Event.Metadata.IsAudit)
                    facts++;
            return facts;
        }
        public async Task<ActorReference> LastActorAsync(string action)
        {
            ActorReference? actor = null;
            var pattern = EventStreamPattern.ForPattern(Tenant.ToString(), action == "mapping" ? "control-criterion-mappings" : "criterion-applicability");
            await foreach (var record in _provider.GetRequiredService<IDomainEventReader>().ReadAsync(pattern, EventCursor.Start, CancellationToken.None))
                actor = record.Event switch
                {
                    CriterionNotApplicableProposed proposed => proposed.Actor,
                    CriterionApplicabilityReviewed reviewed => reviewed.Actor,
                    ControlCriterionMappingReviewed reviewed => reviewed.Actor,
                    _ => actor
                };
            return Assert.IsType<ActorReference>(actor);
        }
        public async Task<Result> DecideAsync(string action, string transport, Uuid? user = null, string criterion = "CC6.2", Uuid? program = null)
        {
            await using var scope = _provider.CreateAsyncScope();
            var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
            var context = Context(user ?? (action == "proposal" ? Author : Reviewer), transport);
            if (action == "proposal")
            {
                var result = await bus.DispatchAsync(new ProposeCriterionNotApplicable(Uuid.CreateVersion4(), Tenant, program ?? Program, Edition, criterion, 0, "Not applicable"), context, CancellationToken.None);
                return result.IsSuccess ? Result.Success : Result.Failure(result.Error);
            }
            IRequest request = action == "mapping" ? new ReviewControlCriterionMapping(Uuid.CreateVersion4(), Tenant, program ?? Program, _decision, 1, "accept", "Verified") :
                new ReviewCriterionApplicability(Uuid.CreateVersion4(), Tenant, program ?? Program, _decision, 1, "accept", "Verified");
            return await bus.DispatchAsync(request, context, CancellationToken.None);
        }
        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            await _sourceProvider.DisposeAsync();
        }
    }
}
