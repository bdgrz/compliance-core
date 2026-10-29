using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
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
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessControl.Reactors;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class TeamCleanupReactorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldRemoveEveryMemberGivenDeletedTeamAndNoProjection()
    {
        // Arrange
        await using var provider = Build();
        var members = Enumerable.Range(0, 205).Select(static _ => Uuid.CreateVersion4()).ToArray();
        foreach (var member in members)
            await AssignAsync(provider, member, Uuid.CreateVersion4());
        await DefineAndDeleteAsync(provider);

        // Act
        await CleanupReactorHost.RunAsync(provider, TenantId, Create);

        // Assert
        foreach (var member in members)
            Assert.False((await CleanupReactorHost.ReadAsync(provider,
                new TeamMember(TenantId, TeamId, member))).IsAssigned);
    }

    [Fact]
    public async Task ShouldBindEachRemovalToTheProvedEpisodeGivenLiveAssignment()
    {
        // Arrange
        await using var provider = Build();
        var member = Uuid.CreateVersion4();
        var episode = Uuid.CreateVersion4();
        await AssignAsync(provider, member, episode);
        await DefineAndDeleteAsync(provider);
        var scenario = new ReactorScenario(new TenantId(TenantId.ToString())).Given(await DeletedFactAsync(provider));

        // Act
        await RunWithRecordingBusAsync(provider, scenario);

        // Assert
        var owner = await CleanupReactorHost.ReadAsync(provider, new TeamMember(TenantId, TeamId, member));
        var removal = Assert.IsType<RemoveTeamMember>(Assert.Single(TypedReactionBus.Sent(scenario)));
        Assert.Equal(TenantId, removal.TenantId);
        Assert.Equal(TeamId, removal.TeamId);
        Assert.Equal(member, removal.MemberId);
        Assert.Equal(episode, removal.MemberEpisodeId);
        Assert.Equal((long)owner.AssignmentRevision, removal.ExpectedAssignmentRevision);
        Assert.Equal((long)owner.AssignmentGeneration, removal.ExpectedAssignmentGeneration);
    }

    [Fact]
    public async Task ShouldReuseRemovalCommandIdsGivenReplayedTeamDeletion()
    {
        // Arrange
        await using var provider = Build();
        await AssignAsync(provider, Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await AssignAsync(provider, Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await DefineAndDeleteAsync(provider);
        var trigger = await DeletedFactAsync(provider);
        var first = new ReactorScenario(new TenantId(TenantId.ToString())).Given(trigger);
        var replay = new ReactorScenario(new TenantId(TenantId.ToString())).Given(trigger);

        // Act
        await RunWithRecordingBusAsync(provider, first);
        await RunWithRecordingBusAsync(provider, replay);

        // Assert
        var removals = TypedReactionBus.Sent(first).OfType<RemoveTeamMember>().ToArray();
        var replayed = TypedReactionBus.Sent(replay).OfType<RemoveTeamMember>().ToArray();
        Assert.Equal(2, removals.Length);
        Assert.Equal(removals, replayed);
        Assert.NotEqual(removals[0].CommandId, removals[1].CommandId);
    }

    [Fact]
    public async Task ShouldDispatchNothingGivenAnAlreadyEmptyTeam()
    {
        // Arrange
        await using var provider = Build();
        var member = Uuid.CreateVersion4();
        await AssignAsync(provider, member, Uuid.CreateVersion4());
        await CleanupReactorHost.SeedAsync(provider, new TeamMember(TenantId, TeamId, member), owner => owner.Remove());
        await DefineAndDeleteAsync(provider);
        var scenario = new ReactorScenario(new TenantId(TenantId.ToString())).Given(await DeletedFactAsync(provider));

        // Act
        await RunWithRecordingBusAsync(provider, scenario);

        // Assert
        Assert.Empty(TypedReactionBus.Sent(scenario));
    }

    [Fact]
    public async Task ShouldIgnoreAssignmentsGivenOtherTeams()
    {
        // Arrange
        await using var provider = Build();
        var otherTeam = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();
        await CleanupReactorHost.SeedAsync(provider,
            new TeamMember(TenantId, otherTeam, member), owner => owner.Assign(Uuid.CreateVersion4()));
        await DefineAndDeleteAsync(provider);

        // Act
        await CleanupReactorHost.RunAsync(provider, TenantId, Create);

        // Assert
        Assert.True((await CleanupReactorHost.ReadAsync(provider, new TeamMember(TenantId, otherTeam, member))).IsAssigned);
    }

    [Fact]
    public async Task ShouldKeepTheNewerAssignmentEpisodeGivenReassignmentAfterTheScan()
    {
        // Arrange
        var store = new ScanHookEventStore();
        await using var provider = Build(store);
        var member = Uuid.CreateVersion4();
        var older = Uuid.CreateVersion4();
        var newer = Uuid.CreateVersion4();
        await AssignAsync(provider, member, older);
        await DefineAndDeleteAsync(provider);
        store.AfterNextScan(EventStreamPattern.ForPattern(TenantId.ToString(), "rbac-team-members"), async () =>
        {
            await CleanupReactorHost.SeedAsync(provider, new TeamMember(TenantId, TeamId, member), owner => owner.Remove());
            await AssignAsync(provider, member, newer);
        });

        // Act
        await CleanupReactorHost.RunAsync(provider, TenantId, Create);

        // Assert
        var current = await CleanupReactorHost.ReadAsync(provider, new TeamMember(TenantId, TeamId, member));
        Assert.True(current.IsAssigned);
        Assert.Equal(newer, current.MembershipEpisodeId);
    }

    static Reactor Create(IProjectionCheckpointStore checkpoints, IRequestBus bus, IDomainEventReader events,
        IAggregateReader reader) => new TeamCleanupReactor(checkpoints, bus, events, reader);

    static ServiceProvider Build(IEventStore? store = null) => RbacManagementServices.Build(allowed: true,
        portia => portia.AddRequestHandler<RemoveTeamMemberHandler>(), store);

    static Task AssignAsync(IServiceProvider provider, Uuid member, Uuid episode) => CleanupReactorHost.SeedAsync(
        provider, new TeamMember(TenantId, TeamId, member), owner => owner.Assign(episode));

    static async Task DefineAndDeleteAsync(IServiceProvider provider)
    {
        await CleanupReactorHost.SeedAsync(provider, new Team(TenantId, TeamId), team => team.Define("Reviewers"));
        await CleanupReactorHost.SeedAsync(provider, new Team(TenantId, TeamId), team => team.Delete());
    }

    static async Task<TeamDeleted> DeletedFactAsync(IServiceProvider provider)
    {
        var records = new List<DomainEventRecord>();
        await foreach (var record in provider.GetRequiredService<IDomainEventReader>()
                           .ReadAsync(EventStreamPattern.ForPattern(TenantId.ToString(), "rbac-teams"),
                               EventCursor.Start, CancellationToken.None))
            records.Add(record);
        return Assert.Single(records.Select(static record => record.Event).OfType<TeamDeleted>());
    }

    static async Task RunWithRecordingBusAsync(ServiceProvider provider, ReactorScenario scenario)
    {
        await using var scope = provider.CreateAsyncScope();
        await scenario.RunAsync(new TeamCleanupReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario), scope.ServiceProvider.GetRequiredService<IDomainEventReader>(),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>()));
    }
}
