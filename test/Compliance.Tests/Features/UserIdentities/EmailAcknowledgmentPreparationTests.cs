using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class EmailAcknowledgmentPreparationTests
{
    [Theory]
    [InlineData("reserve")]
    [InlineData("issue")]
    [InlineData("verify")]
    [InlineData("start")]
    public async Task ShouldReplayOriginalUnitAcknowledgmentGivenNewCandidateContentWritesBecomeUnavailable(string operation)
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var owner = account.UserId;
        const string address = "unit-replay@example.com";
        var challenge = Uuid.CreateVersion4();
        Func<IImmutableContentStore, Task<Result>> run;
        if (operation == "start")
        {
            var request = new StartIdentityRecovery(challenge, account.Email);
            run = async store => await new StartIdentityRecoveryHandler(Operations(host, store), host.Reader,
                host.Ledger, host.Keys, store, host.Clock).HandleAsync(Context(request), CancellationToken.None);
        }
        else if (operation == "reserve")
        {
            var request = new ReserveEmail(Uuid.CreateVersion4(), owner, address, -1);
            run = async store => await new ReserveEmailHandler(Operations(host, store), host.Reader, store, host.Clock)
                .HandleAsync(Context(request), CancellationToken.None);
        }
        else
        {
            var reserved = await host.ReserveHandler().HandleAsync(
                Context(new ReserveEmail(Uuid.CreateVersion4(), owner, address, -1)), CancellationToken.None);
            Assert.True(reserved.IsSuccess, reserved.Error?.Message);
            var issued = new IssueEmailChallenge(challenge, owner, address);
            if (operation == "issue")
            {
                run = async store => await new IssueEmailChallengeHandler(Operations(host, store), host.Keys, store, host.Clock)
                    .HandleAsync(Context(issued), CancellationToken.None);
            }
            else
            {
                Assert.True((await host.IssueEmailHandler().HandleAsync(Context(issued), CancellationToken.None)).IsSuccess);
                var fact = Assert.Single((await host.FactsAsync(new EmailAddress(address).Stream)).OfType<EmailChallengeIssued>());
                var token = host.Keys.Derive(fact.TokenKeyId, challenge, owner, address, fact.ExpiresAt);
                var request = new CompleteEmailChallenge(challenge, owner, address, token);
                run = async store => await new CompleteEmailChallengeHandler(Operations(host, store), host.Reader, store, host.Clock)
                    .HandleAsync(Context(request), CancellationToken.None);
            }
        }
        Assert.True((await run(host.Content)).IsSuccess);
        var before = await host.FactsAsync(new EmailAddress(operation == "start" ? account.Email : address).Stream);
        var unavailable = new UnavailableWrites(host.Content);

        // Act
        var replayed = await run(unavailable);
        var after = await host.FactsAsync(new EmailAddress(operation == "start" ? account.Email : address).Stream);

        // Assert
        Assert.True(replayed.IsSuccess, replayed.Error?.Message);
        Assert.Equal(0, unavailable.Writes);
        Assert.Equal(before.Select(fact => fact.Metadata.EventId), after.Select(fact => fact.Metadata.EventId));
    }

    static NativeOperationExecutor Operations(IdentityRecoveryHost host, IImmutableContentStore store) =>
        new(host.Reader, host.Provider.GetRequiredService<IAggregateExecutor>(), store);

    static RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request, RequestActor.System);

    sealed class UnavailableWrites(IImmutableContentStore inner) : IImmutableContentStore
    {
        public int Writes { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            Writes++;
            throw new ImmutableContentUnavailableException("New candidate content cannot be written after the original admission committed.");
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
