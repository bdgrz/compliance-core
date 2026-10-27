using Bdgrz.Compliance.Features.NativeOperations.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

/// <summary>The two public identity recovery commands: admission on the email address and the frozen acknowledgment.</summary>
public sealed class IdentityRecoveryHandlerTests
{
    [Fact]
    public async Task ShouldReturnSameSuccessGivenUnknownOrUnverifiedAddress()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        const string unverifiedEmail = "unverified@example.com";
        await host.SeedAsync(new EmailAddress(unverifiedEmail), address => address.Reserve(Uuid.CreateVersion4()));

        // Act
        var unknown = await host.StartAsync(Uuid.CreateVersion4(), "unknown@example.com");
        var unverified = await host.StartAsync(Uuid.CreateVersion4(), unverifiedEmail);
        var persisted = await host.AddressAsync(unverifiedEmail);

        // Assert
        Assert.Equal(unknown.IsSuccess, unverified.IsSuccess);
        Assert.True(unknown.IsSuccess);
        Assert.Null(persisted.CurrentRecoveryChallengeId);
    }

    [Fact]
    public async Task ShouldIssueOnceGivenExactStartRetryAndOutstandingChallenge()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var challengeId = Uuid.CreateVersion4();

        // Act
        var started = await host.StartAsync(challengeId);
        var retry = await host.StartAsync(challengeId);
        var another = await host.StartAsync(Uuid.CreateVersion4());
        var facts = await host.FactsAsync(new EmailAddress(account.Email).Stream);

        // Assert
        Assert.True(started.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.True(another.IsSuccess);
        var issued = Assert.Single(facts.OfType<IdentityRecoveryChallengeIssued>());
        Assert.Equal(challengeId, issued.ChallengeId);
        Assert.Equal(challengeId, issued.Operation.LogicalId);
        Assert.Equal("StartIdentityRecovery", issued.Operation.CommandKind);
        Assert.NotNull(issued.FirstResponse);
    }

    [Fact]
    public async Task ShouldIssueAnotherChallengeGivenTheOutstandingOneFailedDeliveryDefinitively()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var first = Uuid.CreateVersion4();
        Assert.True((await host.StartAsync(first)).IsSuccess);
        var attempt = (await host.Ledger.OpenAttemptAsync(first, EmailChallengeDeliveryIntent.Recovery,
            host.Clock.GetUtcNow())).Latest!;
        await host.Ledger.RecordOutcomeAsync(first, attempt.AttemptId, EmailChallengeFailureCertainty.DefinitePermanent,
            "smtp_permanent_rejection", Uuid.CreateVersion4(), host.Clock.GetUtcNow());

        // Act
        var second = Uuid.CreateVersion4();
        var reissued = await host.StartAsync(second);
        var facts = await host.FactsAsync(new EmailAddress(account.Email).Stream);

        // Assert
        Assert.True(reissued.IsSuccess);
        Assert.Equal([first, second], facts.OfType<IdentityRecoveryChallengeIssued>().Select(issued => issued.ChallengeId));
    }

    [Fact]
    public async Task ShouldKeepTheOutstandingChallengeGivenDeliveryStillPendingOrUnknown()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var first = Uuid.CreateVersion4();
        Assert.True((await host.StartAsync(first)).IsSuccess);
        var attempt = (await host.Ledger.OpenAttemptAsync(first, EmailChallengeDeliveryIntent.Recovery,
            host.Clock.GetUtcNow())).Latest!;
        await host.Ledger.RecordOutcomeAsync(first, attempt.AttemptId, EmailChallengeFailureCertainty.Unknown,
            "transport_ambiguous", Uuid.CreateVersion4(), host.Clock.GetUtcNow());

        // Act
        var another = await host.StartAsync(Uuid.CreateVersion4());
        var facts = await host.FactsAsync(new EmailAddress(account.Email).Stream);

        // Assert
        Assert.True(another.IsSuccess);
        Assert.Equal(first, Assert.Single(facts.OfType<IdentityRecoveryChallengeIssued>()).ChallengeId);
    }

    [Fact]
    public async Task ShouldRejectStartGivenEmptyChallengeIdentity()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        await host.SeedAccountAsync();

        // Act
        var result = await host.StartAsync(Uuid.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldClaimChallengeAndAcknowledgeGivenBothProofs()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var challengeId = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, challengeId);

        // Act
        var completed = await host.CompleteAsync(account, challengeId, token);
        var facts = await host.FactsAsync(new EmailAddress(account.Email).Stream);

        // Assert
        Assert.True(completed.IsSuccess);
        Assert.Equal(challengeId, completed.Value.OperationId);
        var claimed = Assert.Single(facts.OfType<IdentityRecoveryChallengeClaimed>());
        Assert.Equal(account.UserId, claimed.UserId);
        Assert.Equal(account.OldIdentityId, claimed.OldIdentityId);
        Assert.Equal(UserIdentity.GetIdentityId(IdentityRecoveryHost.NewProvider, "new-subject"),
            claimed.ReplacementIdentityId);
        Assert.Equal(challengeId, claimed.Operation.LogicalId);
        Assert.Equal("CompleteIdentityRecovery", claimed.Operation.CommandKind);
        Assert.Equal(account.UserId.ToString(), claimed.OriginalActor.Id);
        var intent = await host.IntentAsync(account);
        Assert.Equal(account.UserId, intent.AccountId);
        Assert.Equal(IdentityRecoveryHost.OldProvider, intent.OldProvider);
        Assert.Equal("old-subject", intent.OldSubject);
        Assert.Equal(IdentityRecoveryHost.NewProvider, intent.ReplacementProvider);
        Assert.Equal("new-subject", intent.ReplacementSubject);
    }

    [Fact]
    public async Task ShouldReplayTheFrozenAcknowledgmentGivenExactRetryAfterExpiryAndCompletion()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var challengeId = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, challengeId);
        var first = await host.CompleteAsync(account, challengeId, token);
        var driven = await host.DriveAsync(account);

        // Act
        var retry = await host.CompleteAsync(account, challengeId, token);
        var facts = await host.FactsAsync(new EmailAddress(account.Email).Stream);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.Equal(Bdgrz.Compliance.Features.Processes.ProcessState.Completed, driven.State);
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value, retry.Value);
        Assert.Single(facts.OfType<IdentityRecoveryChallengeClaimed>());
    }

    [Fact]
    public async Task ShouldReplayTheAcknowledgmentGivenRetryWithAnyTokenOnceClaimed()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var challengeId = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, challengeId);
        var first = await host.CompleteAsync(account, challengeId, token);

        // Act
        var retry = await host.CompleteAsync(account, challengeId, new string('f', 64));

        // Assert
        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value, retry.Value);
    }

    [Theory]
    [InlineData("old-identity")]
    [InlineData("replacement-subject")]
    public async Task ShouldConflictGivenChangedIntentUnderTheSameChallengeIdentity(string change)
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var challengeId = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, challengeId);
        await host.CompleteAsync(account, challengeId, token);

        // Act
        var changed = change == "old-identity"
            ? await host.CompleteAsync(account, challengeId, token, oldIdentityId: Uuid.CreateVersion4())
            : await host.CompleteAsync(account, challengeId, token, subject: "attacker-subject");
        var facts = await host.FactsAsync(new EmailAddress(account.Email).Stream);

        // Assert
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error.Kind);
        Assert.Single(facts.OfType<IdentityRecoveryChallengeClaimed>());
        Assert.False((await host.IdentityAsync(UserIdentity.GetIdentityId(IdentityRecoveryHost.NewProvider,
            "attacker-subject"))).IsRegistered);
    }

    [Fact]
    public async Task ShouldRejectInvalidProofsGivenWrongTokenOrForeignOldIdentity()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var other = await host.SeedAccountAsync("other@example.com", "other-subject");
        var challengeId = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, challengeId);

        // Act
        var wrongToken = await host.CompleteAsync(account, challengeId, new string('a', 64));
        var foreign = await host.CompleteAsync(account, challengeId, token, oldIdentityId: other.OldIdentityId);
        var anonymous = await new IdentityRecoveryHandlerProbe(host).CompleteAnonymousAsync(account, challengeId, token);

        // Assert
        Assert.False(wrongToken.IsSuccess);
        Assert.False(foreign.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, foreign.Error.Kind);
        Assert.False(anonymous.IsSuccess);
        Assert.Empty((await host.FactsAsync(new EmailAddress(account.Email).Stream))
            .OfType<IdentityRecoveryChallengeClaimed>());
    }

    [Fact]
    public async Task ShouldKeepBearerTokenOutOfEveryFrozenFactGivenClaim()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var challengeId = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, challengeId);
        await host.CompleteAsync(account, challengeId, token);
        await host.DriveAsync(account);

        // Act
        var persisted = new StringBuilder();
        foreach (var stream in new[]
                 {
                     new EmailAddress(account.Email).Stream, new IdentityRecovery(challengeId).Stream,
                     new UserIdentity(account.OldIdentityId).Stream,
                 })
            foreach (var fact in await host.FactsAsync(stream))
                persisted.Append(JsonSerializer.Serialize(fact, fact.GetType(), ComplianceCoreJsonContext.Default.Options));
        var claimed = (IdentityRecoveryChallengeClaimed)(await host.ClaimRecordAsync(account)).Event;
        foreach (var reference in new[] { claimed.Intent, claimed.VerifiedEmailProof, claimed.VerifiedReplacementProof })
            persisted.Append(Encoding.UTF8.GetString((await host.Content.ReadVerifiedAsync("bdgrz", reference)).Span));

        // Assert
        Assert.DoesNotContain(token, persisted.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRejectStaleIdentityDirectoryGivenMinimumProjectionRevision()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var challengeId = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, challengeId);
        var handler = new GetIdentityRecoveryOptionsHandler(host.Reader,
            new TestIdentityDirectoryReader([new UserIdentityDirectoryEntry(account.UserId, account.OldIdentityId,
                IdentityRecoveryHost.OldProvider, IsRevoked: false)]),
            new CaughtUpIdentityDirectory(), TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetIdentityRecoveryOptions>(
            new GetIdentityRecoveryOptions(account.Email, challengeId, token, MinimumProjectionRevision: 2),
            IdentityRecoveryHost.ProviderActor(IdentityRecoveryHost.NewProvider, "new-subject")), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    sealed class IdentityRecoveryHandlerProbe(IdentityRecoveryHost host)
    {
        public async Task<Result<OperationAcceptedResponse>> CompleteAnonymousAsync(IdentityRecoveryHost.Account account,
            Uuid challengeId, string token)
        {
            var handler = new CompleteIdentityRecoveryHandler(
                new NativeOperationExecutor(host.Reader, host.Provider.GetRequiredService<IAggregateExecutor>(),
                    host.Content), host.Reader, host.Content, TimeProvider.System);
            return await handler.HandleAsync(new RequestContext<CompleteIdentityRecovery>(
                new CompleteIdentityRecovery(account.Email, challengeId, token, account.OldIdentityId),
                new ClaimsPrincipal(new ClaimsIdentity())), CancellationToken.None);
        }
    }

    sealed class TestIdentityDirectoryReader(IReadOnlyList<UserIdentityDirectoryEntry> identities)
        : IUserIdentityDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<UserIdentityDirectoryPage> ListAsync(Uuid userId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new UserIdentityDirectoryPage(1,
                new Page<UserIdentityDirectoryEntry>(identities
                    .Where(identity => identity.UserId == userId && !identity.IsRevoked)
                    .Take(limit).ToArray(), null)));
    }

    sealed class CaughtUpIdentityDirectory : IUserIdentityDirectoryReadConsistency
    {
        public ValueTask<Result> EnsureCaughtUpAsync(CancellationToken ct) =>
            ValueTask.FromResult(Result.Success);
    }
}
