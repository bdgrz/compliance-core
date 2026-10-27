using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TenantManagerPrepareRecoveryTests
{
    [Fact]
    public async Task ShouldReplayActualEqualWinnerGivenLosingGuardIntentPublicationUnavailable()
    {
        // Arrange
        LosingStore content = null!;
        await using var host = new AdministratorTransitionHost(configureContent: inner => content = new LosingStore(inner));
        var actor = await host.AddAdministratorAsync();
        var target = await host.AddAdministratorAsync();
        var command = await host.SuspendRequestAsync(target);
        AdministratorTransitionResponse? original = null;
        content.Arm(async () =>
        {
            var winner = await host.SubmitAsync(actor, command);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            original = winner.Value;
        });
        Result<AdministratorTransitionResponse>? actual = null;

        // Act
        var failure = await Record.ExceptionAsync(async () => actual = await host.SubmitAsync(actor, command));

        // Assert
        Assert.True(content.Failed);
        Assert.NotNull(original);
        Assert.Null(failure);
        Assert.True(actual!.Value.IsSuccess, actual.Value.Error?.Message);
        Assert.Equal(original, actual.Value.Value);
        Assert.Single((await host.GuardFactsAsync()).OfType<TenantManagerWithdrawalAccepted>());
    }

    sealed class LosingStore(IImmutableContentStore inner) : IImmutableContentStore
    {
        Func<Task>? _winner;
        public bool Failed { get; private set; }
        public void Arm(Func<Task> winner) => _winner = winner;
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (!Failed && _winner is { } winner && schema == AdministratorTransitionIntent.Schema)
            {
                Failed = true;
                await winner();
                throw new ImmutableContentUnavailableException("The losing guard candidate is unavailable.");
            }
            return await inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
