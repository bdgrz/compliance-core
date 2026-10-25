using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class IdentityOwnerHistoryTests
{
    [Fact]
    public async Task ShouldExposeEveryHistoricalClaimAndFenceGivenCompactCurrentAndSelectedOwners()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var challenges = new List<Uuid>();
        var original = account.OldIdentityId;
        for (var index = 0; index < 8; index++)
        {
            var challenge = Uuid.CreateVersion4();
            challenges.Add(challenge);
            var subject = $"historical-replacement-{index}";
            var token = await host.StartedAsync(account, challenge);
            Assert.True((await host.CompleteAsync(account, challenge, token, subject: subject)).IsSuccess);
            Assert.Equal(ProcessState.Completed, (await host.DriveAsync(account, challengeId: challenge)).State);
            account = account with { OldIdentityId = UserIdentity.GetIdentityId(IdentityRecoveryHost.NewProvider, subject) };
        }

        // Act
        var selected = await host.AddressAsync(selectedChallengeId: challenges[0]);
        var addressHistory = await EmailAddressHistory.ReadAsync(host.Events, selected);
        var identity = await host.IdentityAsync(original, challenges[0]);
        var identityHistory = await UserIdentityHistory.ReadAsync(host.Events, identity);

        // Assert
        Assert.Equal(challenges, addressHistory.Claims.Keys);
        Assert.Equal(challenges.Count, addressHistory.CompletedRecoveries.Count);
        Assert.All(challenges, id => Assert.Contains(id, addressHistory.CompletedRecoveries));
        Assert.Equal(addressHistory.Claims[challenges[0]], selected.ClaimOf(challenges[0]));
        Assert.True(selected.IsRecoveryChallengeCompleted(challenges[0]));
        Assert.Contains(challenges[0], identityHistory.SettledChallenges);
        Assert.True(identity.IsSettledFor(challenges[0]));
        Assert.Empty(identityHistory.Protections);
    }

    [Theory]
    [InlineData("identity", "payload")]
    [InlineData("address", "payload")]
    [InlineData("identity", "metadata")]
    [InlineData("address", "metadata")]
    public async Task ShouldRejectUnselectedHistoricalPayloadChangeGivenDefaultReaderFinalizesCompleteActualPrefix(string ownerKind, string change)
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var challenge = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, challenge);
        Assert.True((await host.CompleteAsync(account, challenge, token)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await host.DriveAsync(account, challengeId: challenge)).State);
        var owner = ownerKind == "identity" ? (Aggregate)new UserIdentity(account.OldIdentityId) : new EmailAddress(account.Email);
        var reader = new IdentityOwnerAggregateReader(host.Reader, new ChangedHistory(host.Events, owner.Stream, change));

        // Act
        var rejected = await Assert.ThrowsAsync<ImmutableContentIntegrityException>(async () =>
            await reader.HydrateAsync(owner));

        // Assert
        Assert.NotNull(rejected);
        Assert.True(owner.CommittedStreamPosition > 0);
    }

    [Theory]
    [InlineData("offset", false)]
    [InlineData("missing", true)]
    public async Task ShouldDistinguishMissingPrefixFromCorruptPhysicalPrefixGivenInstalledIdentityReader(string change, bool missing)
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var owner = new UserIdentity(account.OldIdentityId);
        var reader = new IdentityOwnerAggregateReader(host.Reader, new ChangedHistory(host.Events, owner.Stream, change));

        // Act
        var error = await Assert.ThrowsAnyAsync<ImmutableContentIntegrityException>(async () =>
            await reader.HydrateAsync(owner));

        // Assert
        Assert.Equal(missing, error is ImmutableContentUnavailableException);
        Assert.Equal(1UL, owner.Version);
        Assert.Equal(1UL, owner.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRequireExplicitHistoricalSelectionGivenCurrentAddressAndIdentityReaders()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var first = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, first);
        Assert.True((await host.CompleteAsync(account, first, token)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await host.DriveAsync(account, challengeId: first)).State);
        var second = Uuid.CreateVersion4();
        await host.StartedAsync(account, second);
        var address = await host.AddressAsync();
        var identity = await host.IdentityAsync(account.OldIdentityId);

        // Act
        var unknownClaim = Assert.Throws<InvalidOperationException>(() => address.ClaimOf(first));
        var unknownCompletion = Assert.Throws<InvalidOperationException>(() => address.IsRecoveryChallengeCompleted(first));
        var unknownSettlement = Assert.Throws<InvalidOperationException>(() => identity.IsSettledFor(first));

        // Assert
        Assert.NotNull(unknownClaim);
        Assert.NotNull(unknownCompletion);
        Assert.NotNull(unknownSettlement);
        Assert.Equal(second, address.CurrentRecoveryChallengeId);
        Assert.False(address.IsRecoveryCompleted);
    }

    sealed class ChangedHistory(IDomainEventReader inner, EventStreamAddress selected, string change) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct = default) => inner.ReadAsync(pattern, cursor, ct);

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong from,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(stream, from, ct))
            {
                if (stream == selected && change == "missing")
                    yield break;
                if (stream == selected && change == "offset")
                {
                    yield return record with { ResourceOffset = record.ResourceOffset + 1 };
                    continue;
                }
                var changed = stream == selected && change is "payload" or "metadata" ? record.Event switch
                {
                    RecoveryIdentityProtectionSettled value => change == "payload" ? value with { Intent = Other(value.Intent) } : value with { },
                    IdentityRecoveryChallengeClaimed value => change == "payload" ? value with { VerifiedEmailProof = Other(value.VerifiedEmailProof) } : value with { },
                    _ => record.Event,
                } : record.Event;
                if (!ReferenceEquals(changed, record.Event))
                    changed.AttachMetadata(change == "metadata"
                        ? record.Event.Metadata with { CausationId = Uuid.CreateVersion4() }
                        : record.Event.Metadata);
                yield return record with { Event = changed };
            }
        }

        static ImmutableContentRef Other(ImmutableContentRef reference) => reference with { Sha256 = new string('0', 64) };
    }
}
