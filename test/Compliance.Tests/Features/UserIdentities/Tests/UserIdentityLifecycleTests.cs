using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Events;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

/// <summary>Registration and revocation decisions of one provider identity: refusals, retries and replay.</summary>
public sealed class UserIdentityLifecycleTests
{
    [Fact]
    public void ShouldRefuseRegistrationWithoutFactGivenRevokedIdentity()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var identity = new UserIdentity("issuer", "revoked-register");
        var scenario = new AggregateScenario<UserIdentity>(identity);
        Assert.True(identity.Register(userId, null).IsSuccess);
        Assert.True(identity.Revoke(Uuid.CreateVersion4(), DateTimeOffset.UtcNow).IsSuccess);
        var pending = scenario.PendingEvents.Count;

        // Act
        var sameUser = identity.Register(userId, null);
        var anonymous = identity.Register(null, null);

        // Assert
        Assert.False(sameUser.IsSuccess);
        Assert.Equal(RequestErrorKind.Unauthorized, sameUser.Error!.Kind);
        Assert.False(anonymous.IsSuccess);
        Assert.Equal(RequestErrorKind.Unauthorized, anonymous.Error!.Kind);
        Assert.Equal(pending, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseRegistrationWithoutFactGivenEmptyUserId()
    {
        // Arrange
        var identity = new UserIdentity("issuer", "empty-user");
        var scenario = new AggregateScenario<UserIdentity>(identity);

        // Act
        var result = identity.Register(Uuid.Empty, null);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.False(identity.IsRegistered);
    }

    [Fact]
    public void ShouldRefuseRevocationWithoutFactGivenUnregisteredIdentity()
    {
        // Arrange
        var identity = new UserIdentity("issuer", "never-registered");
        var scenario = new AggregateScenario<UserIdentity>(identity);

        // Act
        var result = identity.Revoke(Uuid.CreateVersion4(), DateTimeOffset.UtcNow);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.False(identity.IsRevoked);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldRefuseRevocationWithoutFactGivenSelfOrEmptyReplacement(bool useSelf)
    {
        // Arrange
        var identity = new UserIdentity("issuer", "bad-replacement");
        var scenario = new AggregateScenario<UserIdentity>(identity);
        Assert.True(identity.Register(Uuid.CreateVersion4(), null).IsSuccess);
        var pending = scenario.PendingEvents.Count;
        var replacement = useSelf ? identity.Id : Uuid.Empty;

        // Act
        var result = identity.Revoke(replacement, DateTimeOffset.UtcNow);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(pending, scenario.PendingEvents.Count);
        Assert.False(identity.IsRevoked);
    }

    [Fact]
    public void ShouldAcknowledgeRetryAndConflictGivenRevokedIdentityAndChangedReplacement()
    {
        // Arrange
        var identity = new UserIdentity("issuer", "revoke-retry");
        var scenario = new AggregateScenario<UserIdentity>(identity);
        var replacement = Uuid.CreateVersion4();
        Assert.True(identity.Register(Uuid.CreateVersion4(), null).IsSuccess);
        Assert.True(identity.Revoke(replacement, DateTimeOffset.UtcNow).IsSuccess);
        var pending = scenario.PendingEvents.Count;

        // Act
        var retry = identity.Revoke(replacement, DateTimeOffset.UtcNow.AddHours(1));
        var changed = identity.Revoke(Uuid.CreateVersion4(), DateTimeOffset.UtcNow);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Equal(pending, scenario.PendingEvents.Count);
        Assert.Equal(replacement, identity.ReplacementIdentityId);
    }

    [Fact]
    public void ShouldContinueFromReplayedStateGivenRegisteredAndRevokedHistory()
    {
        // Arrange
        var identity = new UserIdentity("issuer", "replayed-revoked");
        var userId = Uuid.CreateVersion4();
        var replacement = Uuid.CreateVersion4();
        var history = new DomainEvent[]
        {
            DomainEventSeed.Attach(new UserIdentityRegistered(userId, "issuer", "replayed-revoked", null), identity.Id, 1),
            DomainEventSeed.Attach(
                new UserIdentityRevoked(identity.Id, userId, replacement, DateTimeOffset.UtcNow), identity.Id, 2),
        };
        var scenario = new AggregateScenario<UserIdentity>(identity).Given(history);

        // Act
        var authenticate = identity.Authenticate();
        var register = identity.Register(userId, null);
        var retry = identity.Revoke(replacement, DateTimeOffset.UtcNow);
        var changed = identity.Revoke(Uuid.CreateVersion4(), DateTimeOffset.UtcNow);

        // Assert
        Assert.True(identity.IsRevoked);
        Assert.Equal(userId, identity.UserId);
        Assert.False(authenticate.IsSuccess);
        Assert.Equal(RequestErrorKind.Unauthorized, authenticate.Error!.Kind);
        Assert.False(register.IsSuccess);
        Assert.Equal(RequestErrorKind.Unauthorized, register.Error!.Kind);
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(2UL, identity.Version);
    }
}
