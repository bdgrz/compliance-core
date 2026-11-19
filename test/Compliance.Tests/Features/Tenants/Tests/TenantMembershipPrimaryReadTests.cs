using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Handlers;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class TenantMembershipPrimaryReadTests
{
    [Fact]
    public async Task ShouldExposePrimaryEpisodeGivenMemberDetailAndListReads()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var member = RbacIds.Member(tenant, user);
        var episode = Uuid.CreateVersion4();
        var designation = Uuid.CreateVersion4();
        var directory = new FitzTenantMembershipDirectoryReader(new InMemoryKvClient());
        await ProjectAsync(directory, tenant,
            MemberFacts.Registered(tenant, member, user, membershipEpisodeId: episode),
            new MemberMarkedPrimary(tenant, member, user, episode, designation));

        // Act
        var detail = await new GetTenantMemberHandler(directory).HandleAsync(
            new RequestContext<GetTenantMember>(new GetTenantMember(tenant, user), RequestActor.System),
            CancellationToken.None);
        var list = await new ListTenantMembersHandler(directory).HandleAsync(
            new RequestContext<ListTenantMembers>(new ListTenantMembers(tenant), RequestActor.System),
            CancellationToken.None);

        // Assert
        Assert.True(detail.IsSuccess);
        Assert.True(detail.Value.IsPrimary);
        Assert.Equal(episode, detail.Value.MembershipEpisodeId);
        Assert.Equal(designation, detail.Value.PrimaryDesignationId);
        var listed = Assert.Single(list.Value.Items);
        Assert.True(listed.IsPrimary);
        Assert.Equal(designation, listed.PrimaryDesignationId);
        Assert.Equal(episode, listed.MembershipEpisodeId);
    }

    [Fact]
    public async Task ShouldExposeGuestProgramCapGivenMemberDetailAndListReads()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var episode = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var directory = new FitzTenantMembershipDirectoryReader(new InMemoryKvClient());
        await ProjectAsync(directory, tenant, MemberFacts.Registered(tenant, RbacIds.Member(tenant, user),
            user, "guest", episode, new DateOnly(2027, 1, 1), [program, program]));

        // Act
        var detail = await new GetTenantMemberHandler(directory).HandleAsync(
            new RequestContext<GetTenantMember>(new GetTenantMember(tenant, user), RequestActor.System),
            CancellationToken.None);
        var list = await new ListTenantMembersHandler(directory).HandleAsync(
            new RequestContext<ListTenantMembers>(new ListTenantMembers(tenant), RequestActor.System),
            CancellationToken.None);

        // Assert
        Assert.Equal(program, Assert.Single(detail.Value.ProgramIds!));
        Assert.Equal(program, Assert.Single(Assert.Single(list.Value.Items).ProgramIds!));
        Assert.False(detail.Value.IsPrimary);
    }

    [Fact]
    public async Task ShouldRetainNewDesignationGivenDelayedPreviousRelease()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var member = RbacIds.Member(tenant, user);
        var episode = Uuid.CreateVersion4();
        var first = Uuid.CreateVersion4();
        var replacement = Uuid.CreateVersion4();
        var directory = new FitzTenantMembershipDirectoryReader(new InMemoryKvClient());
        await ProjectAsync(directory, tenant,
            MemberFacts.Registered(tenant, member, user, membershipEpisodeId: episode),
            new MemberMarkedPrimary(tenant, member, user, episode, first),
            new MemberPrimaryMarkRemoved(tenant, member, user, episode, first),
            new MemberMarkedPrimary(tenant, member, user, episode, replacement));

        // Act
        await ProjectAsync(directory, tenant,
            new MemberPrimaryMarkRemoved(tenant, member, user, episode, first));
        var view = await directory.GetAsync(tenant.ToString(), user);

        // Assert
        Assert.NotNull(view);
        Assert.True(view.IsPrimary);
        Assert.Equal(replacement, view.PrimaryDesignationId);
    }

    [Fact]
    public async Task ShouldIgnorePreviousEpisodePrimaryOutcomeGivenReinvitedMembership()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var member = RbacIds.Member(tenant, user);
        var oldEpisode = Uuid.CreateVersion4();
        var newEpisode = Uuid.CreateVersion4();
        var designation = Uuid.CreateVersion4();
        var directory = new FitzTenantMembershipDirectoryReader(new InMemoryKvClient());
        await ProjectAsync(directory, tenant,
            MemberFacts.Registered(tenant, member, user, membershipEpisodeId: oldEpisode),
            MemberFacts.Deprovisioned(tenant, member, user, Uuid.CreateVersion4(), "Administrator",
                DateTimeOffset.UtcNow, "Membership ended", oldEpisode),
            MemberFacts.Registered(tenant, member, user, membershipEpisodeId: newEpisode));

        // Act
        await ProjectAsync(directory, tenant,
            new MemberMarkedPrimary(tenant, member, user, oldEpisode, designation));
        var view = await directory.GetAsync(tenant.ToString(), user);

        // Assert
        Assert.NotNull(view);
        Assert.Equal(newEpisode, view.MembershipEpisodeId);
        Assert.False(view.IsPrimary);
        Assert.Equal(Uuid.Empty, view.PrimaryDesignationId);
    }

    [Fact]
    public async Task ShouldClearPrimaryExplanationGivenMatchingRelease()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var member = RbacIds.Member(tenant, user);
        var episode = Uuid.CreateVersion4();
        var designation = Uuid.CreateVersion4();
        var directory = new FitzTenantMembershipDirectoryReader(new InMemoryKvClient());
        await ProjectAsync(directory, tenant,
            MemberFacts.Registered(tenant, member, user, membershipEpisodeId: episode),
            new MemberMarkedPrimary(tenant, member, user, episode, designation));

        // Act
        await ProjectAsync(directory, tenant,
            new MemberPrimaryMarkRemoved(tenant, member, user, episode, designation));
        var view = await directory.GetAsync(tenant.ToString(), user);

        // Assert
        Assert.NotNull(view);
        Assert.False(view.IsPrimary);
    }

    [Fact]
    public async Task ShouldMaterializePrimaryMarkGivenProductionMembershipProjector()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var designation = Uuid.CreateVersion4();
        var events = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var member = new Member(tenant, user);
        Assert.True(member.Register().IsSuccess);
        Assert.True(member.MarkPrimary(designation, member.MembershipEpisodeId).IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        var directory = new FitzTenantMembershipDirectoryReader(new InMemoryKvClient());
        var projector = new TenantMembershipProjector(directory);
        await new ProjectorScenario(new TenantId(tenant.ToString())).RunAsync(projector);
        var runner = new ProjectorRunner(events);
        var checkpoint = ProjectionCheckpoint.Start;

        // Act
        while (true)
        {
            var next = await runner.RunAsync(projector, checkpoint);
            if (next == checkpoint)
                break;
            checkpoint = next;
        }
        var result = await directory.GetAsync(tenant.ToString(), user);

        // Assert
        Assert.NotEqual(ProjectionCheckpoint.Start, checkpoint);
        Assert.NotNull(result);
        Assert.True(result.IsPrimary);
        Assert.Equal(designation, result.PrimaryDesignationId);
        Assert.Equal(member.MembershipEpisodeId, result.MembershipEpisodeId);
    }

    static async Task ProjectAsync(FitzTenantMembershipDirectoryReader directory, Uuid tenant,
        params DomainEvent[] facts)
    {
        var identity = new CheckpointIdentity("TenantMembership",
            EventStreamPattern.ForPattern(tenant.ToString()));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        foreach (var fact in facts)
            await directory.ApplyAsync(fact);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
