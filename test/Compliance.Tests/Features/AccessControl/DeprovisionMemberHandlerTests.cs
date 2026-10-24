using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class DeprovisionMemberHandlerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid OtherTenantId = Uuid.CreateVersion4();
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly Uuid ActorUserId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();
    static readonly Uuid GrantId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();
    static readonly DateTimeOffset ActionAt = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldPreserveNewMembershipAuthorityGivenDelayedRetirementCleanup()
    {
        // Arrange
        await using var provider = CreateProvider();
        var reader = provider.GetRequiredService<IAggregateReader>();
        var executor = provider.GetRequiredService<IAggregateExecutor>();
        var memberId = RbacIds.Member(TenantId, UserId);
        var actorMemberId = RbacIds.Member(TenantId, ActorUserId);
        await SeedAsync(provider, new Member(TenantId, UserId), member => member.Register());
        await SeedAsync(provider, new Member(TenantId, UserId), member =>
            member.Deprovision(actorMemberId, "Original administrator", ActionAt, "Prior episode ended."));
        var retired = await reader.HydrateAsync(new Member(TenantId, UserId));
        var termination = Assert.IsType<MembershipTerminationProof>(retired.TerminationProof);
        await SeedAsync(provider, new Member(TenantId, UserId, termination.MembershipEpisodeId), member =>
            member.CompleteDeprovisionCleanup(termination,
                MemberRetirementEffects.Complete(termination).CommandId, ActionAt.AddMinutes(1)));
        await SeedAsync(provider, new Member(TenantId, UserId), member => member.Register());
        var current = await reader.HydrateAsync(new Member(TenantId, UserId));
        await SeedAsync(provider, new TeamMember(TenantId, TeamId, memberId),
            assignment => assignment.Assign(current.MembershipEpisodeId));
        var cleanup = Cleanup(provider);

        // Act
        var completed = await cleanup.CompleteAsync(new RequestDispatchContext(RequestActor.System),
            termination, CancellationToken.None);
        var assignment = await reader.HydrateAsync(new TeamMember(TenantId, TeamId, memberId));
        var membership = await reader.HydrateAsync(new Member(TenantId, UserId));

        // Assert
        Assert.True(completed.IsSuccess);
        Assert.True(assignment.IsAssigned);
        Assert.Equal(current.MembershipEpisodeId, assignment.MembershipEpisodeId);
        Assert.True(membership.IsRegistered);
        Assert.Equal(current.MembershipEpisodeId, membership.MembershipEpisodeId);
    }

    [Fact]
    public async Task ShouldRemoveAuthorityAndRetainHistoryGivenFreshInvitationAfterDeprovisioning()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var provider = host.Provider;
        var reader = host.Reader;
        var actor = await host.AddAdministratorAsync();
        await host.AddAdministratorAsync();
        var member = await host.AddMemberAsync();
        await host.SeedAsync(new TeamMember(host.TenantId, TeamId, member.MemberId),
            assignment => assignment.Assign(member.EpisodeId));
        var terms = new AccessGrantTerms(
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, member.MemberId), RoleId,
            new AccessGrantScope(AccessGrantScopeKind.Program, Uuid.CreateVersion4()),
            new AccessGrantSource("manual", "access-review"),
            ActorReference.ForMember(actor.MemberId, "Alex Admin"), ActionAt, null);
        await host.SeedAsync(new AccessGrant(host.TenantId, GrantId), grant => grant.Issue(terms, member.EpisodeId));
        var cleanup = Cleanup(provider);

        // Act
        var deprovision = await host.DeprovisionAsync(actor, member);
        var process = await host.DriveAsync(deprovision.Value!.OperationId);
        var current = await host.MemberAsync(member);
        var retired = current.TerminationProof!;
        var completed = await cleanup.CompleteAsync(new RequestDispatchContext(RequestActor.System), retired,
            CancellationToken.None);
        var reinvited = await Registration(provider, cleanup).HandleAsync(
            new RequestContext<RegisterMember>(new RegisterMember(Uuid.CreateVersion4(), host.TenantId, member.UserId),
                RequestActor.System), CancellationToken.None);
        var memberAfterInvite = await host.MemberAsync(member);
        var assignmentAfterInvite = await reader.HydrateAsync(new TeamMember(host.TenantId, TeamId, member.MemberId));
        var grantAfterInvite = await reader.HydrateAsync(new AccessGrant(host.TenantId, GrantId));
        var history = await host.FactsAsync(memberAfterInvite.Stream);

        // Assert
        Assert.Equal(AdministratorTransitionResponseState.Accepted, deprovision.Value.State);
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.True(completed.IsSuccess);
        Assert.True(reinvited.IsSuccess);
        Assert.True(memberAfterInvite.IsRegistered);
        Assert.False(memberAfterInvite.IsDeprovisioned);
        Assert.False(assignmentAfterInvite.IsAssigned);
        Assert.True(grantAfterInvite.IsRevoked);
        Assert.Contains(history, item => item is MemberDeprovisioned);
        Assert.Contains(history, item => item is MemberDeprovisionCleanupCompleted);
        Assert.Equal(2, history.Count(item => item is MemberRegistered));
    }

    [Fact]
    public async Task ShouldHideMembershipGivenMemberExistsOnlyInAnotherTenant()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var actor = await host.AddAdministratorAsync();
        await host.SeedAsync(new Member(OtherTenantId, UserId), member => member.Register());

        // Act
        var result = await host.SubmitAsync(actor, new DeprovisionMember(Uuid.CreateVersion4(), host.TenantId, UserId,
            "Access is no longer required.", Uuid.CreateVersion4(), 0));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.False((await host.Reader.HydrateAsync(new Member(host.TenantId, UserId))).IsRegistered);
        Assert.True((await host.Reader.HydrateAsync(new Member(OtherTenantId, UserId))).IsRegistered);
        Assert.Empty(await host.GuardFactsAsync());
    }

    [Fact]
    public async Task ShouldKeepLastAdministratorGivenDeprovisionRequest()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var only = await host.AddAdministratorAsync();
        var other = await host.AddMemberAsync();

        // Act
        var result = await host.DeprovisionAsync(other, only);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.False(result.Error.IsTransient);
        Assert.True((await host.MemberAsync(only)).IsRegistered);
        Assert.Empty(await host.GuardFactsAsync());
    }

    static MemberAuthorityCleanup Cleanup(IServiceProvider provider) => new(
        provider.GetRequiredService<IAggregateReader>(), provider.GetRequiredService<IAggregateExecutor>(),
        provider.GetRequiredService<IDomainEventReader>(), provider.GetRequiredService<IProjectionCheckpointStore>(),
        TimeProvider.System);

    static RegisterMemberHandler Registration(IServiceProvider provider, MemberAuthorityCleanup cleanup)
    {
        var content = provider.GetRequiredService<IImmutableContentStore>();
        return new RegisterMemberHandler(new NativeOperationExecutor(provider.GetRequiredService<IAggregateReader>(),
            provider.GetRequiredService<IAggregateExecutor>(), content),
            provider.GetRequiredService<IAggregateReader>(), cleanup, content, TimeProvider.System);
    }

    static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(
            new PersistenceKvClient(new InMemoryKvClient())));
        services.AddSingleton<IProjectionCheckpointStore, InMemoryProjectionCheckpointStore>();
        services.AddPortia();
        return services.BuildServiceProvider();
    }

    static async Task SeedAsync<TAggregate>(IServiceProvider provider, TAggregate aggregate,
        Func<TAggregate, Result> operation) where TAggregate : Aggregate
    {
        var result = await provider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(aggregate,
            item => AggregateOutcome.Commit(operation(item)), new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        Assert.True(result.IsSuccess);
    }
}
