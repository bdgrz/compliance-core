using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PrimaryOrgAdminProtocolTests
{
    [Fact]
    public void ShouldRefuseChangedOutcomeIdentityGivenCompletedDesignation()
    {
        // Arrange
        var owner = new PrimaryOrgAdmins(Uuid.CreateVersion4());
        var user = Uuid.CreateVersion4();
        var episode = Uuid.CreateVersion4();
        var designation = Uuid.CreateVersion4();
        Assert.True(owner.Designate(designation, user, episode).IsSuccess);
        Assert.True(owner.RecordOutcome(designation, user, episode, null).IsSuccess);
        var scenario = new AggregateScenario<PrimaryOrgAdmins>(owner);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var exact = owner.RecordOutcome(designation, user, episode, null);
        var changedUser = owner.RecordOutcome(designation, Uuid.CreateVersion4(), episode, null);
        var changedEpisode = owner.RecordOutcome(designation, user, Uuid.CreateVersion4(), null);
        var changedOutcome = owner.RecordOutcome(designation, user, episode, "not_active");

        // Assert
        Assert.True(exact.IsSuccess);
        Assert.False(changedUser.IsSuccess);
        Assert.False(changedEpisode.IsSuccess);
        Assert.False(changedOutcome.IsSuccess);
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseSharedDesignationIdentityGivenDifferentPendingMember()
    {
        // Arrange
        var owner = new PrimaryOrgAdmins(Uuid.CreateVersion4());
        var designation = Uuid.CreateVersion4();
        var episode = Uuid.CreateVersion4();
        Assert.True(owner.Designate(designation, Uuid.CreateVersion4(), episode).IsSuccess);
        var scenario = new AggregateScenario<PrimaryOrgAdmins>(owner);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var result = owner.Designate(designation, Uuid.CreateVersion4(), episode);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, owner.SeatCount);
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldKeepOnePrimaryGivenCompetingReleaseAppends()
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var tenant = Uuid.CreateVersion4();
        var users = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var designations = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var owner = new PrimaryOrgAdmins(tenant);
        for (var i = 0; i < 2; i++)
        {
            var episode = Uuid.CreateVersion4();
            Assert.True(owner.Designate(designations[i], users[i], episode).IsSuccess);
            Assert.True(owner.RecordOutcome(designations[i], users[i], episode, null).IsSuccess);
        }
        await writer.SaveAsync(owner, Context());
        var first = await reader.HydrateAsync(new PrimaryOrgAdmins(tenant));
        var second = await reader.HydrateAsync(new PrimaryOrgAdmins(tenant));
        var secondCommand = Uuid.CreateVersion4();
        Assert.True(first.Release(Uuid.CreateVersion4(), users[0], designations[0], "Replace first").IsSuccess);
        Assert.True(second.Release(secondCommand, users[1], designations[1], "Replace second").IsSuccess);

        // Act
        await writer.SaveAsync(first, Context());
        await Assert.ThrowsAsync<EventStreamConcurrencyException>(async () => await writer.SaveAsync(second, Context()));
        var current = await reader.HydrateAsync(new PrimaryOrgAdmins(tenant, secondCommand));
        var retry = current.Release(secondCommand, users[1], designations[1], "Replace second");

        // Assert
        Assert.False(retry.IsSuccess);
        Assert.Equal(1, current.DesignatedCount);
        Assert.Equal(users[1], Assert.Single(current.Seats).UserId);
    }

    [Fact]
    public async Task ShouldCountPendingSeatGivenCompetingDesignationAppends()
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var tenant = Uuid.CreateVersion4();
        var owner = new PrimaryOrgAdmins(tenant);
        var initial = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var episode = Uuid.CreateVersion4();
        Assert.True(owner.Designate(initial, user, episode).IsSuccess);
        Assert.True(owner.RecordOutcome(initial, user, episode, null).IsSuccess);
        await writer.SaveAsync(owner, Context());
        var first = await reader.HydrateAsync(new PrimaryOrgAdmins(tenant));
        var second = await reader.HydrateAsync(new PrimaryOrgAdmins(tenant));
        var secondCommand = Uuid.CreateVersion4();
        var secondUser = Uuid.CreateVersion4();
        Assert.True(first.Designate(Uuid.CreateVersion4(), Uuid.CreateVersion4(), episode).IsSuccess);
        Assert.True(second.Designate(secondCommand, secondUser, episode).IsSuccess);

        // Act
        await writer.SaveAsync(first, Context());
        await Assert.ThrowsAsync<EventStreamConcurrencyException>(async () => await writer.SaveAsync(second, Context()));
        var current = await reader.HydrateAsync(new PrimaryOrgAdmins(tenant, secondCommand));
        var retry = current.Designate(secondCommand, secondUser, episode);

        // Assert
        Assert.False(retry.IsSuccess);
        Assert.Equal(2, current.SeatCount);
        Assert.Equal(1, current.DesignatedCount);
    }

    [Fact]
    public async Task ShouldProtectMemberGivenPrimaryMarkWinsCompetingSuspension()
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var member = new Member(tenant, user);
        Assert.True(member.Register().IsSuccess);
        await writer.SaveAsync(member, Context());
        var marked = await reader.HydrateAsync(new Member(tenant, user));
        var suspended = await reader.HydrateAsync(new Member(tenant, user));
        Assert.True(marked.MarkPrimary(Uuid.CreateVersion4(), marked.MembershipEpisodeId).IsSuccess);
        Assert.True(suspended.Suspend(Uuid.CreateVersion4(), "Administrator", new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), "Access review").IsSuccess);

        // Act
        await writer.SaveAsync(marked, Context());
        await Assert.ThrowsAsync<EventStreamConcurrencyException>(async () => await writer.SaveAsync(suspended, Context()));
        var current = await reader.HydrateAsync(new Member(tenant, user));
        var retry = current.PrepareSuspend(current.MembershipEpisodeId, -1, "Access review");

        // Assert
        Assert.True(current.IsPrimary);
        Assert.False(current.IsSuspended);
        Assert.False(retry.IsSuccess);
    }

    [Fact]
    public async Task ShouldRefuseMarkGivenSuspensionWinsCompetingMemberAppend()
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var member = new Member(tenant, user);
        Assert.True(member.Register().IsSuccess);
        await writer.SaveAsync(member, Context());
        var marked = await reader.HydrateAsync(new Member(tenant, user));
        var suspended = await reader.HydrateAsync(new Member(tenant, user));
        var designation = Uuid.CreateVersion4();
        Assert.True(marked.MarkPrimary(designation, marked.MembershipEpisodeId).IsSuccess);
        Assert.True(suspended.Suspend(Uuid.CreateVersion4(), "Administrator", new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), "Access review").IsSuccess);

        // Act
        await writer.SaveAsync(suspended, Context());
        await Assert.ThrowsAsync<EventStreamConcurrencyException>(async () => await writer.SaveAsync(marked, Context()));
        var current = await reader.HydrateAsync(new Member(tenant, user));
        Assert.True(current.MarkPrimary(designation, current.MembershipEpisodeId).IsSuccess);
        await writer.SaveAsync(current, Context());
        var replayed = await reader.HydrateAsync(new Member(tenant, user));

        // Assert
        Assert.False(replayed.IsPrimary);
        Assert.True(replayed.IsSuspended);
        Assert.Equal("not_active", Assert.IsType<MemberPrimaryMarkRefused>(replayed.PrimaryOutcome(designation)).Reason);
    }

    [Fact]
    public async Task ShouldReplayAcceptedIntentWithoutRestoringSeatGivenRestartAfterRelease()
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var tenant = Uuid.CreateVersion4();
        var owner = new PrimaryOrgAdmins(tenant);
        var users = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var designations = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var episodes = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        for (var i = 0; i < 2; i++)
        {
            Assert.True(owner.Designate(designations[i], users[i], episodes[i]).IsSuccess);
            Assert.True(owner.RecordOutcome(designations[i], users[i], episodes[i], null).IsSuccess);
        }
        var release = Uuid.CreateVersion4();
        Assert.True(owner.Release(release, users[0], designations[0], "Replacement confirmed").IsSuccess);
        await writer.SaveAsync(owner, Context());
        var designationRetry = await reader.HydrateAsync(new PrimaryOrgAdmins(tenant, designations[0]));
        var releaseRetry = await reader.HydrateAsync(new PrimaryOrgAdmins(tenant, release));
        var scenario = new AggregateScenario<PrimaryOrgAdmins>(designationRetry);
        var before = scenario.PendingEvents.ToArray();
        var releaseScenario = new AggregateScenario<PrimaryOrgAdmins>(releaseRetry);

        // Act
        var exactDesignation = designationRetry.Designate(designations[0], users[0], episodes[0]);
        var exactRelease = releaseRetry.Release(release, users[0], designations[0], "Replacement confirmed");
        var changedRelease = releaseRetry.Release(release, users[0], designations[0], "Other reason");

        // Assert
        Assert.True(exactDesignation.IsSuccess);
        Assert.True(exactRelease.IsSuccess);
        Assert.False(changedRelease.IsSuccess);
        Assert.Equal(1, designationRetry.DesignatedCount);
        Assert.True(designationRetry.HasReleased(users[0], episodes[0], designations[0]));
        Assert.False(designationRetry.HasReleased(users[0], Uuid.CreateVersion4(), designations[0]));
        Assert.Equal(before, scenario.PendingEvents);
        Assert.Empty(releaseScenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldRetainNewMarkGivenRestartAndDelayedOldMessages()
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var member = new Member(tenant, user);
        Assert.True(member.Register().IsSuccess);
        var oldDesignation = Uuid.CreateVersion4();
        var nextDesignation = Uuid.CreateVersion4();
        var episode = member.MembershipEpisodeId;
        Assert.True(member.MarkPrimary(oldDesignation, episode).IsSuccess);
        Assert.True(member.UnmarkPrimary(oldDesignation, episode).IsSuccess);
        Assert.True(member.MarkPrimary(nextDesignation, episode).IsSuccess);
        await writer.SaveAsync(member, Context());
        var current = await reader.HydrateAsync(new Member(tenant, user));
        var scenario = new AggregateScenario<Member>(current);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var markRetry = current.MarkPrimary(oldDesignation, episode);
        var unmarkRetry = current.UnmarkPrimary(oldDesignation, episode);
        var changedEpisode = current.MarkPrimary(oldDesignation, Uuid.CreateVersion4());

        // Assert
        Assert.True(markRetry.IsSuccess);
        Assert.True(unmarkRetry.IsSuccess);
        Assert.False(changedEpisode.IsSuccess);
        Assert.Equal(nextDesignation, current.PrimaryDesignationId);
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Fact]
    public async Task ShouldReplayRefusalGivenDesignationTargetsUnregisteredMember()
    {
        // Arrange
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var member = new Member(tenant, user);
        var designation = Uuid.CreateVersion4();
        Assert.True(member.MarkPrimary(designation, Uuid.CreateVersion4()).IsSuccess);
        await writer.SaveAsync(member, Context());

        // Act
        var current = await reader.HydrateAsync(Member.ForVerification(tenant, member.Id));

        // Assert
        Assert.False(current.IsRegistered);
        Assert.False(current.IsPrimary);
        Assert.Equal(user, current.UserId);
        Assert.Equal("not_active", Assert.IsType<MemberPrimaryMarkRefused>(current.PrimaryOutcome(designation)).Reason);
    }

    static RequestDispatchContext Context() => new(RequestActor.System);

    static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        return services.BuildServiceProvider();
    }
}
