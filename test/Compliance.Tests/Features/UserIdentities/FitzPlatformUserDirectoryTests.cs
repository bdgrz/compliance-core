using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class FitzPlatformUserDirectoryTests
{
    [Fact]
    public async Task ShouldUpdateAndClearCurrentNameGivenRetainedProfileObservations()
    {
        // Arrange
        var directory = new FitzPlatformUserDirectory(new InMemoryKvClient());
        var userId = Uuid.CreateVersion4();
        var otherUser = Uuid.CreateVersion4();
        var identity = new CheckpointIdentity("PlatformUserDirectoryV2",
            EventStreamPattern.ForPattern("bdgrz", "user-identities"));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new UserIdentityRegistered(userId, "issuer", "subject", null));
            await directory.ApplyAsync(Observation(userId, "Ada"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Equal("Ada", await directory.ReadAsync(userId));
        Assert.Null(await directory.ReadAsync(otherUser));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(Observation(userId, "Ada King"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        Assert.Equal("Ada King", await directory.ReadAsync(userId));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(Observation(userId, null));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        Assert.Null(await directory.ReadAsync(userId));
        Assert.True(await directory.ExistsAsync(userId));
    }

    [Fact]
    public async Task ShouldFindOnePlatformUserGivenTwoLinkedProviderIdentities()
    {
        // Arrange
        var directory = new FitzPlatformUserDirectory(new InMemoryKvClient());
        var userId = Uuid.CreateVersion4();
        var identity = new CheckpointIdentity("PlatformUserDirectoryV2",
            EventStreamPattern.ForPattern("bdgrz", "user-identities"));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new UserIdentityRegistered(userId,
                "issuer-a", "subject-a", null));
            await directory.ApplyAsync(new UserIdentityRegistered(userId,
                "issuer-b", "subject-b", null));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.True(await directory.ExistsAsync(userId));
        Assert.False(await directory.ExistsAsync(Uuid.CreateVersion4()));
    }

    static UserIdentityProfileObserved Observation(Uuid userId, string? displayName)
    {
        var identity = new UserIdentity("issuer", "subject");
        Assert.True(identity.Register(userId, null).IsSuccess);
        var scenario = new Cntryl.Portia.Testing.AggregateScenario<UserIdentity>(identity);
        // Profile admission follows the committed registration, as in production.
        var registered = Assert.Single(scenario.PendingEvents);
        var source = new UserIdentity("issuer", "subject");
        var admitted = new Cntryl.Portia.Testing.AggregateScenario<UserIdentity>(source).Given(registered);
        Assert.True(source.ObserveAuthenticatedProfile(displayName, DateTimeOffset.UtcNow).IsSuccess);
        return Assert.IsType<UserIdentityProfileObserved>(Assert.Single(admitted.PendingAudits));
    }

}
