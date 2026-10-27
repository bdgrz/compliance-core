using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

public sealed class IdentityRecoveryReceiverPreparationTests
{
    [Theory]
    [InlineData("protect")]
    [InlineData("accept")]
    [InlineData("register")]
    [InlineData("complete")]
    public async Task ShouldReplayActualNativeReceiptBeforeObsoleteDependencyGivenCompletedRecovery(string receiver)
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var challenge = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, challenge);
        Assert.True((await host.CompleteAsync(account, challenge, token)).IsSuccess);
        var completed = await host.DriveAsync(account, challengeId: challenge);
        Assert.Equal(ProcessState.Completed, completed.State);
        var intent = await host.IntentAsync(account, challenge);
        var content = new ObsoleteContent(host.Content, intent.ReplacementProofProvenance, receiver);
        var reader = new ObsoleteOwner(host.Reader, challenge, account.OldIdentityId, receiver);
        var support = new IdentityRecoveryReceiverSupport(host.Provider.GetRequiredService<ISourceFactResolver>(), content,
            host.Provider.GetRequiredService<IAggregateExecutor>(), reader);

        // Act
        var replayed = receiver switch
        {
            "protect" => await new ProtectRecoveryIdentityHandler(support).HandleAsync(
                Context<ProtectRecoveryIdentity>(await host.CommandAsync(completed,
                    IdentityRecoveryReceivers.ProtectRecoveryIdentity, account.OldIdentityId.ToString())), CancellationToken.None),
            "accept" => await new AcceptProtectedIdentityRecoveryHandler(support).HandleAsync(
                Context<AcceptProtectedIdentityRecovery>(await host.CommandAsync(completed,
                    IdentityRecoveryReceivers.AcceptProtectedIdentityRecovery)), CancellationToken.None),
            "register" => await new RegisterRecoveredIdentityHandler(support).HandleAsync(
                Context<RegisterRecoveredIdentity>(await host.CommandAsync(completed,
                    IdentityRecoveryReceivers.RegisterRecoveredIdentity)), CancellationToken.None),
            _ => await new CompleteAcceptedIdentityRecoveryHandler(support).HandleAsync(
                Context<CompleteAcceptedIdentityRecovery>(await host.CommandAsync(completed,
                    IdentityRecoveryReceivers.CompleteAcceptedIdentityRecovery)), CancellationToken.None),
        };

        // Assert
        Assert.True(replayed.IsSuccess, replayed.Error?.Message);
        var receipt = Assert.IsType<ReceiverOutcome.Replayed>(replayed.Value);
        var kind = receiver switch
        {
            "protect" => IdentityRecoveryReceivers.ProtectRecoveryIdentity,
            "accept" => IdentityRecoveryReceivers.AcceptProtectedIdentityRecovery,
            "register" => IdentityRecoveryReceivers.RegisterRecoveredIdentity,
            _ => IdentityRecoveryReceivers.CompleteAcceptedIdentityRecovery,
        };
        Assert.Equal(completed.Facts[ProcessRecord.FactKey(kind, receiver == "protect" ? account.OldIdentityId.ToString() : null)],
            receipt.Fact);
        Assert.Equal(0, content.ObsoleteReads);
        Assert.Equal(0, reader.ObsoleteReads);
    }

    static RequestContext<T> Context<T>(IRequest<ReceiverOutcome> request) where T : class, IRequest<ReceiverOutcome> =>
        new(Assert.IsType<T>(request), RequestActor.System);

    sealed class ObsoleteOwner(IAggregateReader inner, Uuid challenge, Uuid oldIdentity, string receiver) : IAggregateReader
    {
        public int ObsoleteReads { get; private set; }
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            if (receiver == "protect" && aggregate is IdentityRecovery && aggregate.Id == challenge ||
                receiver == "accept" && aggregate is UserIdentity && aggregate.Id == oldIdentity)
            {
                ObsoleteReads++;
                throw new ImmutableContentUnavailableException("The mutable dependency is unavailable after its receiver already committed.");
            }
            return inner.HydrateAsync(aggregate, ct);
        }
    }

    sealed class ObsoleteContent(IImmutableContentStore inner, ImmutableContentRef replacement, string receiver) : IImmutableContentStore
    {
        public int ObsoleteReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (receiver == "complete" && schema == IdentityRecoveryCompletionProofs.Schema)
            {
                ObsoleteReads++;
                throw new ImmutableContentUnavailableException("The obsolete completion candidate write is unavailable.");
            }
            return inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (receiver == "register" && reference == replacement)
            {
                ObsoleteReads++;
                throw new ImmutableContentUnavailableException("The obsolete replacement presentation proof is unavailable.");
            }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
