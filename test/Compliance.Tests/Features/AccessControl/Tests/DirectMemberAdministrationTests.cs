using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

/// <summary>The current routes decide on their own owners; historical guard-first process fixtures remain separate.</summary>
public sealed class DirectMemberAdministrationTests
{
    [Fact]
    public async Task ShouldRemoveOnlyAdministratorTeamAssignmentGivenOrdinaryAdmin()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var person = await host.AddAdministratorAsync();
        var request = await host.RemoveRequestAsync(person);
        var executor = host.Provider.GetRequiredService<IAggregateExecutor>();
        var handler = new RemoveTeamMemberHandler(new NativeOperationExecutor(host.Reader, executor, host.Content),
            host.Content, TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(new RequestContext<RemoveTeamMember>(request,
            AdministratorTransitionHost.Actor(person)), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(AdministratorTransitionResponseState.Completed, result.Value.State);
        Assert.False((await host.AssignmentAsync(person)).IsAssigned);
        Assert.Equal(0, host.GuardReads);
        Assert.Empty(await host.GuardFactsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldChangeOrdinaryAdminMembershipGivenAnotherActingMember(bool deprovision)
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var actor = await host.AddMemberAsync();
        var person = await host.AddAdministratorAsync();
        var member = await host.MemberAsync(person);

        // Act
        var result = await ChangeAsync(host, actor, person, member, deprovision);
        var current = await host.MemberAsync(person);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(AdministratorTransitionResponseState.Completed, result.Value.State);
        Assert.Equal(deprovision, current.IsDeprovisioned);
        Assert.Equal(!deprovision, current.IsSuspended);
        Assert.Equal(0, host.GuardReads);
        Assert.Empty(await host.GuardFactsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseMembershipChangeWithoutFactGivenPrimaryMember(bool deprovision)
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var actor = await host.AddMemberAsync();
        var person = await host.AddAdministratorAsync();
        await host.SeedAsync(new Member(host.TenantId, person.UserId),
            member => member.MarkPrimary(Uuid.CreateVersion4(), person.EpisodeId));
        var before = await host.MemberAsync(person);
        var count = (await host.FactsAsync(before.Stream)).Count;

        // Act
        var result = await ChangeAsync(host, actor, person, before, deprovision);
        var after = await host.MemberAsync(person);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(after.IsPrimary);
        Assert.False(after.IsSuspended);
        Assert.False(after.IsDeprovisioned);
        Assert.Equal(count, (await host.FactsAsync(after.Stream)).Count);
    }

    [Fact]
    public async Task ShouldReplayOriginalResponseGivenExactSuspensionRetryAndRefuseChangedIntent()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var actor = await host.AddMemberAsync();
        var person = await host.AddAdministratorAsync();
        var member = await host.MemberAsync(person);
        var request = new SuspendMember(Uuid.CreateVersion4(), host.TenantId, person.UserId, "Leave",
            person.EpisodeId, (long)member.MemberRevision);
        var executor = host.Provider.GetRequiredService<IAggregateExecutor>();
        var handler = new SuspendMemberHandler(new NativeOperationExecutor(host.Reader, executor, host.Content),
            host.Content, TimeProvider.System);
        var context = new RequestContext<SuspendMember>(request, AdministratorTransitionHost.Actor(actor));

        // Act
        var first = await handler.HandleAsync(context, CancellationToken.None);
        var retry = await handler.HandleAsync(context, CancellationToken.None);
        var changed = await handler.HandleAsync(new RequestContext<SuspendMember>(request with { Reason = "Changed" },
            AdministratorTransitionHost.Actor(actor)), CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value, retry.Value);
        Assert.False(changed.IsSuccess);
        Assert.Single((await host.FactsAsync(member.Stream)).OfType<MemberSuspended>());
    }

    [Fact]
    public async Task ShouldRefuseSuspensionGivenPrimaryMarkCommittedDuringPreparation()
    {
        // Arrange
        CallbackContent? content = null;
        await using var host = new AdministratorTransitionHost(inner => content = new CallbackContent(inner));
        var actor = await host.AddMemberAsync();
        var person = await host.AddAdministratorAsync();
        var before = await host.MemberAsync(person);
        content!.OnIntent = () => host.SeedAsync(new Member(host.TenantId, person.UserId),
            member => member.MarkPrimary(Uuid.CreateVersion4(), person.EpisodeId));

        // Act
        var result = await ChangeAsync(host, actor, person, before, false);
        var current = await host.MemberAsync(person);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(current.IsPrimary);
        Assert.False(current.IsSuspended);
        Assert.Empty((await host.FactsAsync(current.Stream)).OfType<MemberSuspended>());
    }

    [Fact]
    public async Task ShouldKeepRetirementCleanupGivenDirectDeprovision()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var actor = await host.AddMemberAsync();
        var person = await host.AddAdministratorAsync();
        var member = await host.MemberAsync(person);
        var result = await ChangeAsync(host, actor, person, member, true);
        Assert.True(result.IsSuccess);
        var retired = await host.MemberAsync(person);
        var proof = Assert.IsType<MembershipTerminationProof>(retired.TerminationProof);
        var executor = host.Provider.GetRequiredService<IAggregateExecutor>();
        var cleanup = new MemberAuthorityCleanup(host.Reader, executor, host.Events,
            host.Provider.GetRequiredService<IProjectionCheckpointStore>(), TimeProvider.System);
        var request = MemberRetirementEffects.Complete(proof);

        // Act
        var completed = await new CompleteMemberRetirementHandler(cleanup).HandleAsync(
            new RequestContext<CompleteMemberRetirement>(request, RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(completed.IsSuccess);
        Assert.False((await host.AssignmentAsync(person)).IsAssigned);
        Assert.True((await host.MemberAsync(person)).IsDeprovisionCleanupComplete);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseSelfWithdrawalGivenOrdinaryAdministrator(bool deprovision)
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var person = await host.AddAdministratorAsync();
        var before = await host.MemberAsync(person);

        // Act
        var result = await ChangeAsync(host, person, person, before, deprovision);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False((await host.MemberAsync(person)).IsSuspended);
        Assert.False((await host.MemberAsync(person)).IsDeprovisioned);
        Assert.Empty(await host.GuardFactsAsync());
    }

    [Fact]
    public async Task ShouldReinstateWithOriginalResponseGivenExactRetry()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var actor = await host.AddMemberAsync();
        var person = await host.AddAdministratorAsync();
        Assert.True((await ChangeAsync(host, actor, person, await host.MemberAsync(person), false)).IsSuccess);
        var suspended = await host.MemberAsync(person);
        var request = new ReinstateMember(Uuid.CreateVersion4(), host.TenantId, person.UserId,
            person.EpisodeId, (long)suspended.MemberRevision);
        var operations = new NativeOperationExecutor(host.Reader,
            host.Provider.GetRequiredService<IAggregateExecutor>(), host.Content);
        var handler = new ReinstateMemberHandler(operations, host.Content, TimeProvider.System);
        var context = new RequestContext<ReinstateMember>(request, AdministratorTransitionHost.Actor(actor));

        // Act
        var first = await handler.HandleAsync(context, CancellationToken.None);
        var retry = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value, retry.Value);
        Assert.False((await host.MemberAsync(person)).IsSuspended);
        Assert.Single((await host.FactsAsync(suspended.Stream)).OfType<MemberReinstated>());
        Assert.Empty(await host.GuardFactsAsync());
    }

    [Fact]
    public async Task ShouldRetainPrimaryMarkGivenAdministratorTeamRemoval()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var person = await host.AddAdministratorAsync();
        await host.SeedAsync(new Member(host.TenantId, person.UserId),
            member => member.MarkPrimary(Uuid.CreateVersion4(), person.EpisodeId));
        var request = await host.RemoveRequestAsync(person);
        var handler = new RemoveTeamMemberHandler(new NativeOperationExecutor(host.Reader,
            host.Provider.GetRequiredService<IAggregateExecutor>(), host.Content), host.Content, TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(new RequestContext<RemoveTeamMember>(request,
            AdministratorTransitionHost.Actor(person)), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False((await host.AssignmentAsync(person)).IsAssigned);
        Assert.True((await host.MemberAsync(person)).IsPrimary);
        Assert.Empty(await host.GuardFactsAsync());
    }

    static ValueTask<Result<AdministratorTransitionResponse>> ChangeAsync(AdministratorTransitionHost host,
        AdministratorTransitionHost.Person actor, AdministratorTransitionHost.Person person, Member member,
        bool deprovision)
    {
        var operations = new NativeOperationExecutor(host.Reader,
            host.Provider.GetRequiredService<IAggregateExecutor>(), host.Content);
        return deprovision
            ? new DeprovisionMemberHandler(operations, host.Content, TimeProvider.System).HandleAsync(
                new RequestContext<DeprovisionMember>(new DeprovisionMember(Uuid.CreateVersion4(), host.TenantId,
                    person.UserId, "Leave", person.EpisodeId, (long)member.MemberRevision),
                    AdministratorTransitionHost.Actor(actor)), CancellationToken.None)
            : new SuspendMemberHandler(operations, host.Content, TimeProvider.System).HandleAsync(
                new RequestContext<SuspendMember>(new SuspendMember(Uuid.CreateVersion4(), host.TenantId,
                    person.UserId, "Leave", person.EpisodeId, (long)member.MemberRevision),
                    AdministratorTransitionHost.Actor(actor)), CancellationToken.None);
    }

    sealed class CallbackContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        public Func<Task>? OnIntent { get; set; }
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (schema == "member-lifecycle-request.v1" && OnIntent is { } callback)
            {
                OnIntent = null;
                await callback();
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
