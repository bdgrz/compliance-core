using Bdgrz.Compliance.Tests.Testing;
using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

public sealed class EmailAddressTests
{
    [Fact]
    public void ShouldReserveOnceGivenSameOwnerRetry()
    {
        // Arrange
        var owner = Uuid.CreateVersion4();
        var address = new EmailAddress("Person@Example.com");

        // Act
        var scenario = new AggregateScenario<EmailAddress>(address);

        // Assert
        Assert.True(scenario.Aggregate.Reserve(owner).IsSuccess);
        Assert.True(scenario.Aggregate.Reserve(owner).IsSuccess);

        Assert.Equal("EmailAddressReserved", Assert.Single(scenario.PendingEvents).GetType().Name);
        Assert.False(scenario.Aggregate.IsVerified);
    }

    [Fact]
    public void ShouldRejectTakeoverGivenAnotherUserOwnsAddress()
    {
        // Arrange
        var address = new EmailAddress("person@example.com");

        // Act
        var scenario = new AggregateScenario<EmailAddress>(address);

        // Assert
        Assert.True(scenario.Aggregate.Reserve(Uuid.CreateVersion4()).IsSuccess);

        var conflict = scenario.Aggregate.Reserve(Uuid.CreateVersion4());

        Assert.False(conflict.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, conflict.Error.Kind);
        Assert.Single(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldUseDistinctIdsGivenDistinctAddresses()
    {
        // Arrange
        const string firstAddress = "first@example.com";
        const string secondAddress = "second@example.com";

        // Act
        var first = new EmailAddress(firstAddress).Id;
        var second = new EmailAddress(secondAddress).Id;
        var mixedCase = new EmailAddress("Person@Example.com").Id;
        var lowerCase = new EmailAddress("person@example.com").Id;

        // Assert
        Assert.NotEqual(first, second);
        Assert.Equal(mixedCase, lowerCase);
    }

    [Fact]
    public void ShouldAllowCompletionGivenOwnerAndUnexpiredChallenge()
    {
        // Arrange
        var owner = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var address = new EmailAddress("person@example.com");

        // Act
        var scenario = new AggregateScenario<EmailAddress>(address);

        // Assert
        Assert.True(scenario.Aggregate.Reserve(owner).IsSuccess);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("secret")));
        Assert.True(scenario.Aggregate.IssueChallenge(owner, Uuid.CreateVersion4(), hash, now.AddMinutes(15), now)
            .IsSuccess);

        Assert.False(scenario.Aggregate.CompleteChallenge(Uuid.CreateVersion4(), "secret", now).IsSuccess);
        Assert.False(scenario.Aggregate.CompleteChallenge(owner, "wrong", now).IsSuccess);
        Assert.False(scenario.Aggregate.CompleteChallenge(owner, "secret", now.AddMinutes(15)).IsSuccess);
        Assert.False(scenario.Aggregate.IsVerified);

        Assert.True(scenario.Aggregate.CompleteChallenge(owner, "secret", now.AddMinutes(1)).IsSuccess);
        Assert.True(scenario.Aggregate.IsVerified);
        Assert.Equal(3, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldInvalidateOldTokenGivenReissuedChallenge()
    {
        // Arrange
        var owner = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var address = new EmailAddress("person@example.com");

        // Act
        var scenario = new AggregateScenario<EmailAddress>(address);

        // Assert
        Assert.True(scenario.Aggregate.Reserve(owner).IsSuccess);
        var oldHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("old")));
        var newHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("new")));
        Assert.True(scenario.Aggregate.IssueChallenge(owner, Uuid.CreateVersion4(), oldHash, now.AddMinutes(15), now)
            .IsSuccess);
        Assert.True(scenario.Aggregate.IssueChallenge(owner, Uuid.CreateVersion4(), newHash, now.AddMinutes(15), now)
            .IsSuccess);

        Assert.False(scenario.Aggregate.CompleteChallenge(owner, "old", now).IsSuccess);
        Assert.True(scenario.Aggregate.CompleteChallenge(owner, "new", now).IsSuccess);
    }

    [Fact]
    public void ShouldReportLedgerDeliveryStatusOfCurrentChallengeOnlyGivenReissue()
    {
        // Arrange
        var owner = Uuid.CreateVersion4();
        var firstId = Uuid.CreateVersion4();
        var secondId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var address = new EmailAddress("person@example.com");
        var scenario = new AggregateScenario<EmailAddress>(address);
        Assert.True(address.Reserve(owner).IsSuccess);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("secret")));

        // Act
        Assert.True(address.IssueChallenge(owner, firstId, hash, now.AddMinutes(15), now, "key-1").IsSuccess);
        var pending = address.GetChallengeStatus(now);
        var failed = address.GetChallengeStatus(now, "failed");
        Assert.True(address.IssueChallenge(owner, secondId, hash, now.AddMinutes(15), now, "key-1").IsSuccess);
        var reissued = address.GetChallengeStatus(now);

        // Assert
        Assert.Equal("pending", pending.DeliveryStatus);
        Assert.Equal("failed", failed.DeliveryStatus);
        Assert.Equal("pending", reissued.DeliveryStatus);
        Assert.Equal(secondId, address.CurrentChallengeId);
        Assert.Equal("expired", address.GetChallengeStatus(now.AddMinutes(15), "delivered").DeliveryStatus);
        Assert.DoesNotContain(scenario.PendingEvents, fact => fact.GetType().Name.Contains("Delivery"));
    }

    [Fact]
    public void ShouldRejectReuseOfChallengeIdentityGivenIssuedChallenge()
    {
        // Arrange
        var owner = Uuid.CreateVersion4();
        var challengeId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var address = new EmailAddress("person@example.com");
        var scenario = new AggregateScenario<EmailAddress>(address);
        Assert.True(address.Reserve(owner).IsSuccess);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("secret")));
        Assert.True(address.IssueChallenge(owner, challengeId, hash, now.AddMinutes(15), now).IsSuccess);

        // Act
        var reuse = address.IssueChallenge(owner, challengeId, hash, now.AddMinutes(15), now);

        // Assert
        Assert.False(reuse.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, reuse.Error.Kind);
        Assert.Equal(2, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldRequireVerifiedOwnedEmailGivenRecoveryChallenge()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var address = new EmailAddress("person@example.com");
        Assert.True(address.Reserve(userId).IsSuccess);

        // Act
        var result = address.IssueRecovery(userId, Uuid.CreateVersion4(),
            new string('a', 64), now.AddMinutes(15), now, "key-1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }

    [Fact]
    public void ShouldBindRecoveryChallengeToOneReplacementGivenValidEmailProof()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var oldIdentityId = Uuid.CreateVersion4();
        var replacementIdentityId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var recoveryToken = new string('A', 64);
        var address = new EmailAddress("person@example.com");
        Assert.True(address.Reserve(userId).IsSuccess);
        var emailTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("verified")));
        Assert.True(address.IssueChallenge(userId, Uuid.CreateVersion4(), emailTokenHash,
            now.AddMinutes(15), now).IsSuccess);
        Assert.True(address.CompleteChallenge(userId, "verified", now).IsSuccess);
        var challengeId = Uuid.CreateVersion4();
        var recoveryHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(recoveryToken)));
        Assert.True(address.IssueRecovery(userId, challengeId, recoveryHash,
            now.AddMinutes(15), now, "key-1").IsSuccess);

        // Act
        var claimed = address.Claim(userId, challengeId, recoveryToken,
            oldIdentityId, replacementIdentityId, now.AddMinutes(1));
        var second = address.Claim(userId, challengeId, recoveryToken,
            oldIdentityId, replacementIdentityId, now.AddMinutes(2));
        var retargeted = address.Claim(userId, challengeId, recoveryToken,
            oldIdentityId, Uuid.CreateVersion4(), now.AddMinutes(2));
        var retargetedAfterExpiry = address.Claim(userId, challengeId, recoveryToken,
            oldIdentityId, Uuid.CreateVersion4(), now.AddMinutes(16));

        // Assert
        Assert.True(claimed.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.False(retargeted.IsSuccess);
        Assert.False(retargetedAfterExpiry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, second.Error.Kind);
        Assert.Equal(RequestErrorKind.Conflict, retargeted.Error.Kind);
        Assert.Equal(replacementIdentityId, address.RecoveryReplacementIdentityId);
        Assert.True(address.IsVerified);
        Assert.Equal(challengeId, address.CurrentRecoveryChallengeId);
    }

    [Fact]
    public void ShouldRejectMalformedRecoveryTokenGivenProofValidation()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var address = new EmailAddress("person@example.com");
        Assert.True(address.Reserve(userId).IsSuccess);
        const string verifiedToken = "verified";
        Assert.True(address.IssueChallenge(userId, Uuid.CreateVersion4(),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(verifiedToken))),
            now.AddMinutes(15), now).IsSuccess);
        Assert.True(address.CompleteChallenge(userId, verifiedToken, now).IsSuccess);
        const string recoveryToken = "recovery-token";
        var challengeId = Uuid.CreateVersion4();
        Assert.True(address.IssueRecovery(userId, challengeId,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(recoveryToken))),
            now.AddMinutes(15), now, "key-1").IsSuccess);

        // Act
        var malformedToken = address.ValidateIdentityRecoveryChallenge(userId, challengeId,
            recoveryToken, now);
        var oversizedToken = address.ValidateIdentityRecoveryChallenge(userId, challengeId,
            new string('A', 1_000_000), now);
        var nonHexToken = address.ValidateIdentityRecoveryChallenge(userId, challengeId,
            new string('Z', 64), now);

        // Assert
        Assert.False(malformedToken.IsSuccess);
        Assert.False(oversizedToken.IsSuccess);
        Assert.False(nonHexToken.IsSuccess);
    }
}
