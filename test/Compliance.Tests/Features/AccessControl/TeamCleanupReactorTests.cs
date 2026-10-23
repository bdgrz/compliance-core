using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TeamCleanupReactorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldRemoveMembersGivenDeletedTeam()
    {
        // Arrange
        var firstMember = Uuid.CreateVersion4();
        var secondMember = Uuid.CreateVersion4();
        var members = new FakeTeamMemberDirectoryReader(
            new TeamMemberView(TeamId, firstMember), new TeamMemberView(TeamId, secondMember));
        var scenario = new ReactorScenario().Given(new TeamDeleted(TenantId, TeamId));

        // Act
        await scenario.RunAsync(new TeamCleanupReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario), members));

        // Assert
        Assert.Equal(2, TypedReactionBus.Sent(scenario).Count);
        Assert.Contains(TypedReactionBus.Sent(scenario), request =>
            request is RemoveTeamMember removal && removal.MemberId == firstMember);
        Assert.Contains(TypedReactionBus.Sent(scenario), request =>
            request is RemoveTeamMember removal && removal.MemberId == secondMember);
        Assert.All(TypedReactionBus.Sent(scenario), request =>
        {
            var removal = Assert.IsType<RemoveTeamMember>(request);
            Assert.Equal(TenantId, removal.TenantId);
            Assert.Equal(TeamId, removal.TeamId);
        });
    }

    [Fact]
    public async Task ShouldReuseRemovalCommandIdsGivenReplayedTeamDeletion()
    {
        // Arrange
        var members = new FakeTeamMemberDirectoryReader(new TeamMemberView(TeamId, Uuid.CreateVersion4()),
            new TeamMemberView(TeamId, Uuid.CreateVersion4()));
        var scenario = new ReactorScenario().Given(new TeamDeleted(TenantId, TeamId));

        // Act
        await scenario.RunAsync(new TeamCleanupReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario), members));
        await scenario.RunAsync(new TeamCleanupReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario), members));

        // Assert
        var removals = TypedReactionBus.Sent(scenario).OfType<RemoveTeamMember>().ToArray();
        Assert.Equal(4, removals.Length);
        Assert.Equal(removals[0], removals[2]);
        Assert.Equal(removals[1], removals[3]);
        Assert.NotEqual(removals[0].CommandId, removals[1].CommandId);
    }

    [Fact]
    public async Task ShouldDispatchNothingGivenAnAlreadyEmptyTeam()
    {
        // Arrange
        var members = new FakeTeamMemberDirectoryReader();
        var scenario = new ReactorScenario().Given(new TeamDeleted(TenantId, TeamId));

        // Act
        await scenario.RunAsync(new TeamCleanupReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario), members));

        // Assert
        Assert.Equal([TeamId], members.QueriedTeams);
        Assert.Empty(TypedReactionBus.Sent(scenario));
    }

    sealed class FakeTeamMemberDirectoryReader(params TeamMemberView[] members) : ITeamMemberDirectoryReader
    {
        public List<Uuid> QueriedTeams { get; } = [];

        public ValueTask<Page<TeamMemberView>> ListAsync(
            Uuid tenantId,
            Uuid teamId,
            int? limit,
            string? cursor,
            string? search,
            bool descending,
            CancellationToken ct = default) =>
            Record(teamId, new Page<TeamMemberView>(members, null));

        ValueTask<Page<TeamMemberView>> Record(Uuid teamId, Page<TeamMemberView> page)
        {
            QueriedTeams.Add(teamId);
            return ValueTask.FromResult(page);
        }
    }
}
