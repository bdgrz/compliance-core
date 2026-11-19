using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class FitzPlatformOperatorDirectoryTests
{
    [Fact]
    public async Task ShouldPageActiveOperatorsGivenManyIndependentUserStreams()
    {
        // Arrange
        var directory = new FitzPlatformOperatorDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("PlatformOperatorDirectory",
            EventStreamPattern.ForPattern("bdgrz", "platform-operators"));
        var expected = new HashSet<Uuid>();
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            for (var index = 0; index < 201; index++)
            {
                var userId = Uuid.CreateVersion4();
                expected.Add(userId);
                var granted = new PlatformOperatorBootstrapAccessGranted(userId, Uuid.CreateVersion4(), 0,
                    Uuid.CreateVersion4(), new ActorAttribution("portia:system", "Portia"));
                granted.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), PlatformOperator.IdentityOf(userId),
                    1, DateTimeOffset.UtcNow));
                await directory.ApplyAsync(granted);
            }
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var first = await directory.ListAsync(200, null);
        var second = await directory.ListAsync(200, first.NextCursor);

        // Assert
        Assert.Equal(200, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        Assert.Single(second.Items);
        Assert.Null(second.NextCursor);
        Assert.True(expected.SetEquals(first.Items.Concat(second.Items).Select(item => item.UserId)));
        Assert.All(first.Items.Concat(second.Items), item => Assert.False(item.IsPrimary));
    }
}
