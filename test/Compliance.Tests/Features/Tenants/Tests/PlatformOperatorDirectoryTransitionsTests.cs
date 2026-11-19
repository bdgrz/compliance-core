using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Tenants.Projections;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class PlatformOperatorDirectoryTransitionsTests
{
    [Fact]
    public void ShouldTrackOnlyCurrentGrantAndExactPrimaryMarkGivenLifecycleFacts()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var lineage = Uuid.CreateVersion4();
        var reservation = Uuid.CreateVersion4();
        var actor = new ActorAttribution("portia:system", "Portia");
        var bootstrap = new PlatformOperatorBootstrapGrantAndMarkAccepted(user, Uuid.CreateVersion4(), 0,
            grant, lineage, 1, reservation, actor);
        var unmark = new PlatformOperatorPrimaryUnmarked(user, Uuid.CreateVersion4(), 1, grant,
            lineage, 1, reservation, Uuid.CreateVersion4());
        var revoke = new PlatformOperatorAccessRevoked(user, Uuid.CreateVersion4(), 2, grant,
            user, new ActorAttribution(user.ToString(), "operator"), "revoke");

        // Act
        var marked = PlatformOperatorDirectoryTransitions.Apply(null, bootstrap);
        var unmarked = PlatformOperatorDirectoryTransitions.Apply(marked, unmark);
        var removed = PlatformOperatorDirectoryTransitions.Apply(unmarked, revoke);

        // Assert
        Assert.NotNull(marked);
        Assert.Equal(grant, marked.GrantEpisodeId);
        Assert.Equal(1UL, marked.PrimaryGeneration);
        Assert.NotNull(unmarked);
        Assert.Equal(0UL, unmarked.PrimaryGeneration);
        Assert.Null(removed);
    }

    [Fact]
    public void ShouldRefusePrimaryMarkGivenNoActiveGrant()
    {
        // Arrange
        var mark = new PlatformOperatorPrimaryMarkAccepted(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, Uuid.CreateVersion4());

        // Act
        // Assert
        Assert.Throws<InvalidOperationException>(() => PlatformOperatorDirectoryTransitions.Apply(null, mark));
    }

    [Fact]
    public void ShouldRefuseUnmarkGivenDifferentReservationOrGrantEpisode()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var grant = Uuid.CreateVersion4();
        var lineage = Uuid.CreateVersion4();
        var reservation = Uuid.CreateVersion4();
        var row = new PlatformOperatorDirectoryRow(user, grant, lineage, 2, reservation);
        var wrongReservation = new PlatformOperatorPrimaryUnmarked(user, Uuid.CreateVersion4(), 1, grant,
            lineage, 2, Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var wrongGrant = wrongReservation with { GrantEpisodeId = Uuid.CreateVersion4(), ReservationEventId = reservation };

        // Act
        // Assert
        Assert.Throws<InvalidOperationException>(() => PlatformOperatorDirectoryTransitions.Apply(row, wrongReservation));
        Assert.Throws<InvalidOperationException>(() => PlatformOperatorDirectoryTransitions.Apply(row, wrongGrant));
    }

    [Fact]
    public void ShouldCreateOrdinaryRowGivenBootstrapAccessWithoutPrimaryMark()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var fact = new PlatformOperatorBootstrapAccessGranted(user, operation, 0, Uuid.CreateVersion4(),
            new ActorAttribution("portia:system", "Portia"));

        // Act
        var row = PlatformOperatorDirectoryTransitions.Apply(null, fact);

        // Assert
        Assert.NotNull(row);
        Assert.Equal(operation, row.GrantEpisodeId);
        Assert.Equal(0UL, row.PrimaryGeneration);
    }
}
