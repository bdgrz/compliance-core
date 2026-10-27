using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class IdentityRecoveryAdmissionPreparationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayFrozenAcknowledgmentGivenEqualClaimCommitsDuringExpectedIdentityReadFailure(bool missing)
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var challenge = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, challenge);
        OperationAcceptedResponse? winner = null;
        var reader = new DuringIdentityRead(host.Reader, account.OldIdentityId, async () =>
        {
            var accepted = await host.CompleteAsync(account, challenge, token);
            Assert.True(accepted.IsSuccess, accepted.Error?.Message);
            winner = accepted.Value;
            throw missing
                ? new ImmutableContentUnavailableException("The exact identity dependency is temporarily unavailable.")
                : new ImmutableContentIntegrityException("The candidate identity dependency does not verify.");
        });
        var handler = Handler(host, reader);

        // Act
        var result = await handler.HandleAsync(Context(account, challenge, token), CancellationToken.None);
        var facts = await host.FactsAsync(new EmailAddress(account.Email).Stream);

        // Assert
        Assert.True(reader.Triggered);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(winner, result.Value);
        Assert.Single(facts.OfType<IdentityRecoveryChallengeClaimed>());
    }

    [Fact]
    public async Task ShouldWaitAtExactPreparedCutGivenNewCurrentChallengeCommitsDuringIdentityPreparation()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var first = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, first);
        var second = Uuid.CreateVersion4();
        var reader = new DuringIdentityRead(host.Reader, account.OldIdentityId, async () =>
        {
            host.Clock.Advance(TimeSpan.FromMinutes(16));
            await host.StartedAsync(account, second);
        });
        var handler = Handler(host, reader);

        // Act
        var result = await handler.HandleAsync(Context(account, first, token), CancellationToken.None);
        var freshRetry = await handler.HandleAsync(Context(account, first, token), CancellationToken.None);
        var address = await host.AddressAsync();
        var facts = await host.FactsAsync(address.Stream);

        // Assert
        Assert.True(reader.Triggered);
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
        Assert.False(freshRetry.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, freshRetry.Error.Kind);
        Assert.False(freshRetry.Error.IsTransient);
        Assert.Equal(second, address.CurrentRecoveryChallengeId);
        Assert.DoesNotContain(facts, fact => fact is IdentityRecoveryChallengeClaimed);
    }

    static CompleteIdentityRecoveryHandler Handler(IdentityRecoveryHost host, IAggregateReader reader) =>
        new(new NativeOperationExecutor(reader, host.Provider.GetRequiredService<IAggregateExecutor>(), host.Content),
            reader, host.Content, host.Clock);

    static RequestContext<CompleteIdentityRecovery> Context(IdentityRecoveryHost.Account account, Uuid challenge, string token) =>
        new(new CompleteIdentityRecovery(account.Email, challenge, token, account.OldIdentityId),
            IdentityRecoveryHost.ProviderActor(IdentityRecoveryHost.NewProvider, "new-subject"));

    sealed class DuringIdentityRead(IAggregateReader inner, Uuid identityId, Func<Task> during) : IAggregateReader
    {
        public bool Triggered { get; private set; }

        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            if (!Triggered && aggregate is UserIdentity && aggregate.Id == identityId)
            {
                Triggered = true;
                await during();
            }
            return await inner.HydrateAsync(aggregate, ct);
        }
    }
}
