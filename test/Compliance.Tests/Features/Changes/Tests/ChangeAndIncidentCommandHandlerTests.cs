using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Changes.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
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
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
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
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.Changes.Handlers;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.Changes.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Changes.Tests;

public sealed class ChangeAndIncidentCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 15, 0, 0, TimeSpan.Zero);
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid SystemId = Uuid.CreateVersion4();
    static readonly Uuid SystemVersionId = Uuid.CreateVersion4();
    static readonly Uuid EvidenceId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldRecordReviseAndAssessSignificantChangeGivenCompleteDomainContent()
    {
        // Arrange
        await using var scenario = new Scenario();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var changeId = Uuid.CreateVersion4();
        var content = new SignificantChangeContent("change_control", "CHG-104", null,
            "Production identity update", "Replace the production identity integration.",
            new DateOnly(2026, 10, 18), null, RbacIds.Member(TenantId, author), [SystemId],
            "high", "restricted", [EvidenceId]);
        var revision = content with { Description = "Replace the production identity integration and reapprove its privileged access." };
        var assessment = new SignificantChangeImpactContent("significant",
            "The identity integration changes an in-scope key control.", false, false, true, false,
            ["control"], [new("system_instance", SystemId, SystemVersionId, 4)], [], [], [],
            [EvidenceId], false);

        // Act
        var recorded = await new RecordSignificantChangeHandler(scenario.ChangeOperations).HandleAsync(
            Scenario.Context(new RecordSignificantChange(TenantId, ProgramId, changeId, content), author),
            CancellationToken.None);
        var revised = await new ReviseSignificantChangeHandler(scenario.ChangeOperations).HandleAsync(
            Scenario.Context(new ReviseSignificantChange(Uuid.CreateVersion4(), TenantId, ProgramId, changeId, 1, revision,
                "Clarified the privileged access impact."), author), CancellationToken.None);
        var assessed = await new AssessSignificantChangeHandler(scenario.ChangeOperations).HandleAsync(
            Scenario.Context(new AssessSignificantChange(Uuid.CreateVersion4(), TenantId, ProgramId, changeId, 2, assessment), reviewer),
            CancellationToken.None);
        var staleAssessment = await new AssessSignificantChangeHandler(scenario.ChangeOperations).HandleAsync(
            Scenario.Context(new AssessSignificantChange(Uuid.CreateVersion4(), TenantId, ProgramId, changeId, 1,
                assessment with { Rationale = "A conflicting stale assessment." }), reviewer), CancellationToken.None);
        var aggregate = await scenario.Changes.RulesAsync(TenantId, ProgramId, changeId);

        // Assert
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        Assert.True(assessed.IsSuccess, assessed.Error?.Message);
        Assert.False(staleAssessment.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, staleAssessment.Error!.Kind);
        Assert.Equal("assessed", aggregate.Current!.Status);
        Assert.Equal(revision.Description, aggregate.Current.Current!.Content.Description);
        Assert.Equal(2, aggregate.Current.CurrentVersionNumber);
        Assert.Equal(RbacIds.Member(TenantId, reviewer), aggregate.Current.CurrentAssessment!.AssessedByMemberId);
        Assert.Equal("significant", aggregate.Current.CurrentAssessment.Significance);
    }

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _services;
        readonly AsyncServiceScope _scope;

        public Scenario()
        {
            var services = new ServiceCollection();
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddPortia();
            _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _services.CreateAsyncScope();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Changes = new SignificantChangeReader(Reader, events, Content);
            ChangeOperations = new SignificantChangeOperations(new NativeOperationExecutor(Reader, Executor, Content), Changes, Content, Clock);
        }

        public IImmutableContentStore Content { get; } = ChangeSeeds.Content();
        public SignificantChangeReader Changes { get; }
        public SignificantChangeOperations ChangeOperations { get; }
        public TimeProvider Clock { get; } = new FixedTimeProvider(Now);
        public IAggregateExecutor Executor { get; }
        public IAggregateReader Reader { get; }

        public static RequestContext<TRequest> Context<TRequest>(TRequest request, Uuid userId)
            where TRequest : IRequestBase => new(request, new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()), new Claim("name", "Test user")],
                "UnitTest")));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _services.DisposeAsync();
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
