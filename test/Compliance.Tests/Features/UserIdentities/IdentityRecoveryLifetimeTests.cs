using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class IdentityRecoveryLifetimeTests
{
    [Fact]
    public async Task ShouldKeepIdentityAuthorityBoundedAndFenceEarliestProtectionGivenManyRealRejectedRecoveries()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        IRequest<ReceiverOutcome>? earliestProtection = null;
        Uuid earliest = default;
        for (var index = 0; index < 16; index++)
        {
            var challenge = Uuid.CreateVersion4();
            var subject = $"occupied-{index}";
            var token = await host.StartedAsync(account, challenge);
            Assert.True((await host.CompleteAsync(account, challenge, token, subject: subject)).IsSuccess);
            var process = await host.PrepareProcessAsync(account, challenge);
            var materialized = Assert.IsType<ReceiverOutcome.Applied>((await host.SendAsync(await host.CommandAsync(process,
                IdentityRecoveryReceivers.MaterializeIdentityRecoveryPreparation))).Value);
            process = process with
            {
                Facts = new Dictionary<string, NativeFactReference>(process.Facts)
                {
                    [ProcessRecord.FactKey(IdentityRecoveryReceivers.MaterializeIdentityRecoveryPreparation, null)] = materialized.Fact,
                },
            };
            if (index == 0)
            {
                earliest = challenge;
                earliestProtection = await host.CommandAsync(process, IdentityRecoveryReceivers.ProtectRecoveryIdentity,
                    account.OldIdentityId.ToString());
            }
            await host.RegisterElsewhereAsync(IdentityRecoveryHost.NewProvider, subject);
            Assert.Equal(ProcessState.Aborted, (await host.DriveAsync(account, challengeId: challenge)).State);
            host.Clock.Advance(TimeSpan.FromMinutes(16));
        }

        // Act
        var delayed = await host.SendAsync(earliestProtection!);
        var identity = await host.IdentityAsync(account.OldIdentityId, earliest);
        var facts = await host.FactsAsync(identity.Stream);

        // Assert
        Assert.True(delayed.IsSuccess, delayed.Error?.Message);
        Assert.True(delayed.Value is ReceiverOutcome.Replayed or ReceiverOutcome.Superseded);
        Assert.True(identity.IsSettledFor(earliest));
        Assert.False(identity.IsProtectedFor(earliest));
        Assert.False(identity.IsRevoked);
        Assert.Equal(16, facts.OfType<RecoveryIdentityProtectionSettled>().Count());
        Assert.DoesNotContain(facts, fact => fact is UserIdentityRevoked);
        AssertBounded(identity);
    }

    [Fact]
    public async Task ShouldKeepAddressAuthorityBoundedAndHistoricalNoticeIndependentGivenManyRealSuccessfulRecoveries()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        ProcessRecord? earliestProcess = null;
        Uuid earliest = default;
        Uuid earliestOld = default;
        Uuid earliestReplacement = default;
        for (var index = 0; index < 16; index++)
        {
            var challenge = Uuid.CreateVersion4();
            var subject = $"replacement-{index}";
            var token = await host.StartedAsync(account, challenge);
            Assert.True((await host.CompleteAsync(account, challenge, token, subject: subject)).IsSuccess);
            var process = await host.DriveAsync(account, challengeId: challenge);
            Assert.Equal(ProcessState.Completed, process.State);
            var replacement = UserIdentity.GetIdentityId(IdentityRecoveryHost.NewProvider, subject);
            if (index == 0)
            {
                earliest = challenge;
                earliestProcess = process;
                earliestOld = account.OldIdentityId;
                earliestReplacement = replacement;
            }
            account = account with { OldIdentityId = replacement };
        }
        var current = Uuid.CreateVersion4();
        await host.StartedAsync(account, current);

        // Act
        var completion = await host.SendAsync(await host.CommandAsync(earliestProcess!,
            IdentityRecoveryReceivers.CompleteEmailRecoveryChallenge, earliestProcess!.Targets[0]));
        var notice = await host.SendAsync(await host.CommandAsync(earliestProcess!,
            IdentityRecoveryReceivers.SendIdentityRecoveryNotice, earliestProcess!.Targets[0]));
        var address = await host.AddressAsync(selectedChallengeId: earliest);
        var historical = address.ClaimOf(earliest);
        var facts = await host.FactsAsync(address.Stream);

        // Assert
        Assert.IsType<ReceiverOutcome.Replayed>(completion.Value);
        Assert.IsType<ReceiverOutcome.Performed>(notice.Value);
        Assert.NotNull(historical);
        Assert.Equal(earliestOld, historical.OldIdentityId);
        Assert.Equal(earliestReplacement, historical.ReplacementIdentityId);
        Assert.True(address.IsRecoveryChallengeCompleted(earliest));
        Assert.Equal(current, address.CurrentRecoveryChallengeId);
        Assert.False(address.IsRecoveryCompleted);
        Assert.False(address.IsRecoveryChallengeCompleted(current));
        Assert.Equal((earliest, account.UserId, account.Email), host.Delivery.Notices[^1]);
        Assert.Equal(16, facts.OfType<IdentityRecoveryChallengeClaimed>().Count());
        Assert.Equal(16, facts.OfType<IdentityRecoveryChallengeCompleted>().Count());
        AssertBounded(address);
    }

    [Theory]
    [InlineData("source", true)]
    [InlineData("intent", true)]
    [InlineData("corrupt_source", false)]
    [InlineData("corrupt_intent", false)]
    public async Task ShouldClassifyExpectedDependencyFailureGivenRealRecoveryPreparation(string unavailable, bool transient)
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var request = await PreparationAsync(host);
        var support = Support(host, unavailable, request.Intent);

        // Act
        var result = await new MaterializeIdentityRecoveryPreparationHandler(support).HandleAsync(
            new RequestContext<MaterializeIdentityRecoveryPreparation>(request, RequestActor.System), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.Equal(transient, result.Error.IsTransient);
        Assert.Equal(IdentityRecovery.Stage.None, (await host.RecoveryAsync(request.ChallengeId)).State);
    }

    [Fact]
    public async Task ShouldReplayEqualWinnerGivenPreparationIntentBecomesUnavailableAfterActualAcceptance()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var request = await PreparationAsync(host);
        var first = await host.SendAsync(request);
        var accepted = Assert.IsType<ReceiverOutcome.Applied>(first.Value);
        var support = Support(host, "intent", request.Intent);

        // Act
        var replayed = await new MaterializeIdentityRecoveryPreparationHandler(support).HandleAsync(
            new RequestContext<MaterializeIdentityRecoveryPreparation>(request, RequestActor.System), CancellationToken.None);
        var facts = await host.FactsAsync(new IdentityRecovery(request.ChallengeId).Stream);

        // Assert
        Assert.True(replayed.IsSuccess, replayed.Error?.Message);
        Assert.Equal(accepted.Fact, Assert.IsType<ReceiverOutcome.Replayed>(replayed.Value).Fact);
        Assert.Single(facts.OfType<IdentityRecoveryPrepared>());
    }

    [Fact]
    public async Task ShouldObserveCancellationGivenRealRecoveryPreparationDependencyCancels()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var request = await PreparationAsync(host);
        using var cancelled = new CancellationTokenSource();
        var support = new IdentityRecoveryReceiverSupport(
            new InterruptedSource(host.Provider.GetRequiredService<ISourceFactResolver>(), cancelled), host.Content,
            host.Provider.GetRequiredService<IAggregateExecutor>(), host.Reader);

        // Act
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new MaterializeIdentityRecoveryPreparationHandler(support).HandleAsync(
                new RequestContext<MaterializeIdentityRecoveryPreparation>(request, RequestActor.System), cancelled.Token));

        // Assert
        Assert.True(cancelled.IsCancellationRequested);
        Assert.Equal(cancelled.Token, exception.CancellationToken);
        Assert.Equal(IdentityRecovery.Stage.None, (await host.RecoveryAsync(request.ChallengeId)).State);
    }

    static async Task<MaterializeIdentityRecoveryPreparation> PreparationAsync(IdentityRecoveryHost host)
    {
        var account = await host.SeedAccountAsync();
        var challenge = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, challenge);
        Assert.True((await host.CompleteAsync(account, challenge, token)).IsSuccess);
        return Assert.IsType<MaterializeIdentityRecoveryPreparation>(await host.CommandAsync(
            await host.PrepareProcessAsync(account, challenge), IdentityRecoveryReceivers.MaterializeIdentityRecoveryPreparation));
    }

    static IdentityRecoveryReceiverSupport Support(IdentityRecoveryHost host, string failure, ImmutableContentRef intent) =>
        new(new UnavailableSource(host.Provider.GetRequiredService<ISourceFactResolver>(), failure),
            new UnavailableIntent(host.Content, intent, failure), host.Provider.GetRequiredService<IAggregateExecutor>(), host.Reader);

    static void AssertBounded(Aggregate owner) => Assert.All(owner.GetType().GetFields(
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly), field =>
        Assert.False(field.GetValue(owner) is IEnumerable retained and not string && retained.Cast<object>().Skip(2).Any(),
            $"The recovery command owner retains lifetime history in {field.Name}."));

    sealed class UnavailableSource(ISourceFactResolver inner, string failure) : ISourceFactResolver
    {
        public Task<ResolvedSourceFact> ResolveAsync(EventStreamAddress stream, ulong position, CancellationToken ct = default) =>
            inner.ResolveAsync(stream, position, ct);
        public Task<VerifiedSourceProof<T>> VerifyAsync<T>(SourceFactProof<T> proof, CancellationToken ct = default) where T : DomainEvent =>
            failure switch
            {
                "source" => throw new SourceFactUnavailableException("The exact committed recovery source is not visible yet."),
                "corrupt_source" => throw new SourceFactVerificationException("The exact committed recovery source is corrupt."),
                _ => inner.VerifyAsync(proof, ct),
            };
    }

    sealed class UnavailableIntent(IImmutableContentStore inner, ImmutableContentRef selected, string failure) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            reference == selected && failure == "intent"
                ? throw new ImmutableContentUnavailableException("The exact admitted recovery intent is not visible yet.")
                : reference == selected && failure == "corrupt_intent"
                    ? throw new ImmutableContentIntegrityException("The admitted recovery intent is corrupt.")
                    : inner.ReadVerifiedAsync(realm, reference, ct);
    }

    sealed class InterruptedSource(ISourceFactResolver inner, CancellationTokenSource cancelled) : ISourceFactResolver
    {
        public Task<ResolvedSourceFact> ResolveAsync(EventStreamAddress stream, ulong position, CancellationToken ct = default) =>
            inner.ResolveAsync(stream, position, ct);
        public Task<VerifiedSourceProof<T>> VerifyAsync<T>(SourceFactProof<T> proof, CancellationToken ct = default) where T : DomainEvent
        {
            cancelled.Cancel();
            ct.ThrowIfCancellationRequested();
            return inner.VerifyAsync(proof, ct);
        }
    }
}
