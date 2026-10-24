using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class RoleTeamContentOperationTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReplayWithoutAppendingGivenAnExactTeamDefinitionRetryAfterDeletion()
    {
        // Arrange
        await using var host = new Host();
        var teamId = Uuid.CreateVersion4();
        var define = new DefineTeam(TenantId, teamId, "Reviewers");
        Assert.True((await host.SendAsync(define)).IsSuccess);
        Assert.True((await host.SendAsync(new DeleteTeam(Uuid.CreateVersion4(), TenantId, teamId))).IsSuccess);
        var before = (await host.FactsAsync("rbac-teams")).Count;

        // Act
        var retry = await host.SendAsync(define);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(before, (await host.FactsAsync("rbac-teams")).Count);
    }

    [Fact]
    public async Task ShouldConflictGivenTheSameTeamIdentityWithChangedContent()
    {
        // Arrange
        await using var host = new Host();
        var teamId = Uuid.CreateVersion4();
        Assert.True((await host.SendAsync(new DefineTeam(TenantId, teamId, "Reviewers"))).IsSuccess);

        // Act
        var changed = await host.SendAsync(new DefineTeam(TenantId, teamId, "Auditors"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        var defined = Assert.Single((await host.FactsAsync("rbac-teams")).OfType<TeamDefined>());
        Assert.Equal("Reviewers", defined.Name);
    }

    [Fact]
    public async Task ShouldConflictGivenTheSameBuiltInRoleIdentityWithChangedContent()
    {
        // Arrange
        await using var host = new Host();
        var roleId = BuiltInRbac.ViewerRoleId(TenantId);
        Assert.True((await host.SendAsync(new DefineRole(TenantId, roleId, BuiltInRbac.ViewerRoleName),
            RequestActor.System)).IsSuccess);

        // Act
        var replay = await host.SendAsync(new DefineRole(TenantId, roleId, BuiltInRbac.ViewerRoleName),
            RequestActor.System);
        var changed = await host.SendAsync(new DefineRole(TenantId, roleId, "Read only"), RequestActor.System);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Single((await host.FactsAsync("rbac-roles")).OfType<RoleDefined>());
    }

    [Fact]
    public async Task ShouldRefuseRoleDefinitionGivenANonSystemActor()
    {
        // Arrange
        await using var host = new Host();

        // Act
        var result = await host.SendAsync(new DefineRole(TenantId, BuiltInRbac.ViewerRoleId(TenantId), "Viewer"));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(await host.FactsAsync("rbac-roles"));
    }

    [Fact]
    public async Task ShouldConflictGivenTheSameRenameIdentityWithAnotherName()
    {
        // Arrange
        await using var host = new Host();
        var roleId = BuiltInRbac.ViewerRoleId(TenantId);
        Assert.True((await host.SendAsync(new DefineRole(TenantId, roleId, BuiltInRbac.ViewerRoleName),
            RequestActor.System)).IsSuccess);
        var commandId = Uuid.CreateVersion4();
        var rename = new RenameRole(commandId, TenantId, roleId, "Read only");
        Assert.True((await host.SendAsync(rename, RequestActor.System)).IsSuccess);
        var before = (await host.FactsAsync("rbac-roles")).Count;

        // Act
        var retry = await host.SendAsync(rename, RequestActor.System);
        var changed = await host.SendAsync(new RenameRole(commandId, TenantId, roleId, "Observer"), RequestActor.System);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(before, (await host.FactsAsync("rbac-roles")).Count);
    }

    [Fact]
    public async Task ShouldAppendNothingGivenARenameToTheCurrentName()
    {
        // Arrange
        await using var host = new Host();
        var roleId = BuiltInRbac.ViewerRoleId(TenantId);
        Assert.True((await host.SendAsync(new DefineRole(TenantId, roleId, BuiltInRbac.ViewerRoleName),
            RequestActor.System)).IsSuccess);

        // Act
        var result = await host.SendAsync(
            new RenameRole(Uuid.CreateVersion4(), TenantId, roleId, BuiltInRbac.ViewerRoleName), RequestActor.System);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(await host.FactsAsync("rbac-roles"));
    }

    [Fact]
    public async Task ShouldReplayAnExactRoleDeletionRetryAndAppendNothingGivenASecondDeletion()
    {
        // Arrange
        await using var host = new Host();
        var roleId = Uuid.CreateVersion4();
        await host.SeedAsync(new Role(TenantId, roleId), role => role.Define("Reviewer"));
        var delete = new DeleteRole(Uuid.CreateVersion4(), TenantId, roleId);
        Assert.True((await host.SendAsync(delete)).IsSuccess);

        // Act
        var retry = await host.SendAsync(delete);
        var second = await host.SendAsync(new DeleteRole(Uuid.CreateVersion4(), TenantId, roleId));

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Single((await host.FactsAsync("rbac-roles")).OfType<RoleDeleted>());
    }

    [Fact]
    public async Task ShouldConflictGivenTheSameDeletionIdentityFromAnotherActor()
    {
        // Arrange
        await using var host = new Host();
        var teamId = Uuid.CreateVersion4();
        await host.SeedAsync(new Team(TenantId, teamId), team => team.Define("Reviewers"));
        var delete = new DeleteTeam(Uuid.CreateVersion4(), TenantId, teamId);
        Assert.True((await host.SendAsync(delete)).IsSuccess);

        // Act
        var other = await host.SendAsync(delete, RbacManagementServices.Actor());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, other.Error?.Kind);
        Assert.Single((await host.FactsAsync("rbac-teams")).OfType<TeamDeleted>());
    }

    [Fact]
    public async Task ShouldRefuseDeletionGivenABuiltInTeamAndAnUndefinedTeam()
    {
        // Arrange
        await using var host = new Host();
        await host.SeedAsync(new Team(TenantId, BuiltInRbac.ViewersTeamId(TenantId)),
            team => team.Define(BuiltInRbac.ViewersTeamName));

        // Act
        var builtIn = await host.SendAsync(new DeleteTeam(Uuid.CreateVersion4(), TenantId,
            BuiltInRbac.ViewersTeamId(TenantId)));
        var undefined = await host.SendAsync(new DeleteTeam(Uuid.CreateVersion4(), TenantId, Uuid.CreateVersion4()));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, builtIn.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, undefined.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectDeletionGivenACommandIdEqualToTheTeamId()
    {
        // Arrange
        await using var host = new Host();
        var teamId = Uuid.CreateVersion4();
        await host.SeedAsync(new Team(TenantId, teamId), team => team.Define("Reviewers"));

        // Act
        var result = await host.SendAsync(new DeleteTeam(teamId, TenantId, teamId));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    sealed class Host : IAsyncDisposable
    {
        readonly ServiceProvider _provider = RbacManagementServices.Build(allowed: true, portia => portia
            .AddRequestHandler<DefineTeamHandler>().AddRequestHandler<DeleteTeamHandler>()
            .AddRequestHandler<DefineRoleHandler>().AddRequestHandler<RenameRoleHandler>()
            .AddRequestHandler<DeleteRoleHandler>());
        readonly AsyncServiceScope _scope;

        public Host() => _scope = _provider.CreateAsyncScope();

        public ValueTask<Result> SendAsync(IRequest request, ClaimsPrincipal? actor = null) =>
            _scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
                new RequestDispatchContext(actor ?? FixedActor), CancellationToken.None);

        public Task SeedAsync<T>(T owner, Func<T, Result> operation) where T : Aggregate =>
            RbacManagementServices.SeedAsync(_provider, owner, operation);

        public async Task<List<DomainEvent>> FactsAsync(string area)
        {
            var facts = new List<DomainEvent>();
            await foreach (var record in _scope.ServiceProvider.GetRequiredService<IDomainEventReader>().ReadAsync(
                               EventStreamPattern.ForPattern(TenantId.ToString(), area), EventCursor.Start,
                               CancellationToken.None))
                facts.Add(record.Event);
            return facts;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    static readonly ClaimsPrincipal FixedActor = RbacManagementServices.Actor();
}
