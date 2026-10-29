using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
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
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

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
