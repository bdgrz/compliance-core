using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

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
