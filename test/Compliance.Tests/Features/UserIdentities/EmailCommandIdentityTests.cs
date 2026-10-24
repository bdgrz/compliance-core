using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

/// <summary>The caller-chosen identities of the email address commands: challenge and command ids, and their native facts.</summary>
public sealed class EmailCommandIdentityTests
{
    const string Email = "owner@example.com";

    [Fact]
    public async Task ShouldIssueWithTheCallerChallengeIdentityOnceGivenExactRetry()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var owner = Uuid.CreateVersion4();
        await host.SeedAsync(new EmailAddress(Email), address => address.Reserve(owner));
        var challengeId = Uuid.CreateVersion4();
        var handler = host.IssueEmailHandler();
        var request = new IssueEmailChallenge(challengeId, owner, Email);

        // Act
        var first = await handler.HandleAsync(Context(request, owner), CancellationToken.None);
        var retry = await handler.HandleAsync(Context(request, owner), CancellationToken.None);
        var issued = (await host.FactsAsync(new EmailAddress(Email).Stream)).OfType<EmailChallengeIssued>().ToArray();

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        var fact = Assert.Single(issued);
        Assert.Equal(challengeId, fact.ChallengeId);
        Assert.Equal(challengeId, fact.Operation.LogicalId);
        Assert.NotNull(fact.FirstResponse);
    }

    [Fact]
    public async Task ShouldConflictGivenChangedUserOnTheSameChallengeIdentity()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var owner = Uuid.CreateVersion4();
        await host.SeedAsync(new EmailAddress(Email), address => address.Reserve(owner));
        var challengeId = Uuid.CreateVersion4();
        var handler = host.IssueEmailHandler();
        await handler.HandleAsync(Context(new IssueEmailChallenge(challengeId, owner, Email), owner), CancellationToken.None);

        // Act
        var changed = await handler.HandleAsync(
            Context(new IssueEmailChallenge(challengeId, Uuid.CreateVersion4(), Email), owner), CancellationToken.None);

        // Assert
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error.Kind);
        Assert.Single((await host.FactsAsync(new EmailAddress(Email).Stream)).OfType<EmailChallengeIssued>());
    }

    [Fact]
    public async Task ShouldRejectIssueGivenEmptyChallengeIdentity()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var owner = Uuid.CreateVersion4();
        await host.SeedAsync(new EmailAddress(Email), address => address.Reserve(owner));

        // Act
        var result = await host.IssueEmailHandler().HandleAsync(
            Context(new IssueEmailChallenge(Uuid.Empty, owner, Email), owner), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldRejectVerificationGivenAnotherChallengeIdentity()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var owner = Uuid.CreateVersion4();
        await host.SeedAsync(new EmailAddress(Email), address => address.Reserve(owner));
        var challengeId = Uuid.CreateVersion4();
        await host.IssueEmailHandler().HandleAsync(
            Context(new IssueEmailChallenge(challengeId, owner, Email), owner), CancellationToken.None);
        var issued = (await host.FactsAsync(new EmailAddress(Email).Stream)).OfType<EmailChallengeIssued>().Single();
        var token = host.Keys.Derive(issued.TokenKeyId, challengeId, owner, Email, issued.ExpiresAt);
        var handler = host.VerifyEmailHandler();

        // Act
        var other = await handler.HandleAsync(Context(new CompleteEmailChallenge(Uuid.CreateVersion4(), owner, Email, token), owner),
            CancellationToken.None);
        var exact = await handler.HandleAsync(Context(new CompleteEmailChallenge(challengeId, owner, Email, token), owner),
            CancellationToken.None);

        // Assert
        Assert.False(other.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, other.Error.Kind);
        Assert.True(exact.IsSuccess);
        Assert.True((await host.AddressAsync(Email)).IsVerified);
    }

    [Fact]
    public async Task ShouldReplayVerificationGivenExactRetryOnlyWithTheTokenAndNeverRetainTheToken()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var owner = Uuid.CreateVersion4();
        await host.SeedAsync(new EmailAddress(Email), address => address.Reserve(owner));
        var challengeId = Uuid.CreateVersion4();
        await host.IssueEmailHandler().HandleAsync(
            Context(new IssueEmailChallenge(challengeId, owner, Email), owner), CancellationToken.None);
        var issued = (await host.FactsAsync(new EmailAddress(Email).Stream)).OfType<EmailChallengeIssued>().Single();
        var token = host.Keys.Derive(issued.TokenKeyId, challengeId, owner, Email, issued.ExpiresAt);
        var handler = host.VerifyEmailHandler();
        var request = new CompleteEmailChallenge(challengeId, owner, Email, token);

        // Act
        var first = await handler.HandleAsync(Context(request, owner), CancellationToken.None);
        host.Clock.Advance(TimeSpan.FromHours(2));
        var retry = await handler.HandleAsync(Context(request, owner), CancellationToken.None);
        var wrongToken = await handler.HandleAsync(Context(request with { Token = new string('0', 64) }, owner),
            CancellationToken.None);
        var facts = await host.FactsAsync(new EmailAddress(Email).Stream);
        var verified = Assert.Single(facts.OfType<EmailAddressVerified>());
        var serialized = JsonSerializer.Serialize(verified, ComplianceCoreJsonContext.Default.EmailAddressVerified);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(wrongToken.IsSuccess);
        Assert.DoesNotContain(token, serialized, StringComparison.Ordinal);
        Assert.Equal(AuthoredIntentDigest.Compute(new CompleteEmailChallengeAuthoredIntent(challengeId, owner, Email)),
            verified.Operation.AuthoredIntentSha256);
        Assert.NotEqual(AuthoredIntentDigest.Compute(request), verified.Operation.AuthoredIntentSha256);
    }

    [Fact]
    public async Task ShouldReserveWithTheCallerCommandIdentityGivenExactRetryAfterLaterRevisions()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var owner = Uuid.CreateVersion4();
        var commandId = Uuid.CreateVersion4();
        var handler = host.ReserveHandler();
        var request = new ReserveEmail(commandId, owner, Email, 0);

        // Act
        var first = await handler.HandleAsync(Context(request, owner), CancellationToken.None);
        await host.IssueEmailHandler().HandleAsync(
            Context(new IssueEmailChallenge(Uuid.CreateVersion4(), owner, Email), owner), CancellationToken.None);
        var retry = await handler.HandleAsync(Context(request, owner), CancellationToken.None);
        var changed = await handler.HandleAsync(Context(request with { UserId = Uuid.CreateVersion4() }, owner),
            CancellationToken.None);
        var reserved = Assert.Single((await host.FactsAsync(new EmailAddress(Email).Stream)).OfType<EmailAddressReserved>());

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error.Kind);
        Assert.Equal(commandId, reserved.Operation.LogicalId);
        Assert.Equal(0UL, reserved.Operation.OriginalExpectedRevision);
    }

    [Fact]
    public async Task ShouldRejectReserveGivenStaleExpectedRevision()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var owner = Uuid.CreateVersion4();
        await host.SeedAsync(new EmailAddress(Email), address => address.Reserve(owner));
        var handler = host.ReserveHandler();

        // Act
        var stale = await handler.HandleAsync(Context(new ReserveEmail(Uuid.CreateVersion4(), owner, Email, 0), owner),
            CancellationToken.None);
        var current = await handler.HandleAsync(Context(new ReserveEmail(Uuid.CreateVersion4(), owner, Email, 1), owner),
            CancellationToken.None);
        var system = await handler.HandleAsync(Context(new ReserveEmail(Uuid.CreateVersion4(), owner, Email, -1), owner),
            CancellationToken.None);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error.Kind);
        Assert.True(current.IsSuccess);
        Assert.True(system.IsSuccess);
    }

    static RequestContext<TRequest> Context<TRequest>(TRequest request, Uuid userId) where TRequest : IRequestBase =>
        new(request, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession")));
}
