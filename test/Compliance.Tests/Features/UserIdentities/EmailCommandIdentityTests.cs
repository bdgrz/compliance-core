using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

/// <summary>The caller-chosen identities of the email address commands: challenge and command ids.</summary>
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
        var handler = new IssueEmailChallengeHandler(host.Provider.GetRequiredService<IAggregateExecutor>(), host.Keys,
            host.Clock);
        var request = new IssueEmailChallenge(challengeId, owner, Email);

        // Act
        var first = await handler.HandleAsync(Context(request, owner), CancellationToken.None);
        var retry = await handler.HandleAsync(Context(request, owner), CancellationToken.None);
        var issued = (await host.FactsAsync(new EmailAddress(Email).Stream)).OfType<EmailChallengeIssued>().ToArray();

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(challengeId, Assert.Single(issued).ChallengeId);
    }

    [Fact]
    public async Task ShouldRejectIssueGivenEmptyChallengeIdentity()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var owner = Uuid.CreateVersion4();
        await host.SeedAsync(new EmailAddress(Email), address => address.Reserve(owner));
        var handler = new IssueEmailChallengeHandler(host.Provider.GetRequiredService<IAggregateExecutor>(), host.Keys,
            host.Clock);

        // Act
        var result = await handler.HandleAsync(Context(new IssueEmailChallenge(Uuid.Empty, owner, Email), owner),
            CancellationToken.None);

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
        var executor = host.Provider.GetRequiredService<IAggregateExecutor>();
        await new IssueEmailChallengeHandler(executor, host.Keys, host.Clock).HandleAsync(
            Context(new IssueEmailChallenge(challengeId, owner, Email), owner), CancellationToken.None);
        var issued = (await host.FactsAsync(new EmailAddress(Email).Stream)).OfType<EmailChallengeIssued>().Single();
        var token = host.Keys.Derive(issued.TokenKeyId!, challengeId, owner, Email, issued.ExpiresAt);
        var handler = new CompleteEmailChallengeHandler(executor, host.Clock);

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
    public async Task ShouldRejectReserveGivenStaleExpectedRevision()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var owner = Uuid.CreateVersion4();
        await host.SeedAsync(new EmailAddress(Email), address => address.Reserve(owner));
        var handler = new ReserveEmailHandler(host.Provider.GetRequiredService<IAggregateExecutor>());

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
