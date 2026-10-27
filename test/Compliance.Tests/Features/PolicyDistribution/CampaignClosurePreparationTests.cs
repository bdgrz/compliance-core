using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Handlers;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution;

public sealed class CampaignClosurePreparationTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldReplayClosureReceiptOrRequestedCancellationGivenWinnerBeforeIntentReadFailure(bool abort,
        bool cancel)
    {
        // Arrange
        await using var host = new CampaignHost();
        var commands = await SeedAsync(host);
        using var cancellation = new CancellationTokenSource();
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new FaultStore(host.Content, abort, async () =>
        {
            winner = await HandleAsync(host, commands, abort, host.Content, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var actual = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            actual = await HandleAsync(host, commands, abort, fault, cancellation.Token));

        // Assert
        Assert.True(winner.IsSuccess);
        var original = Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact;
        Assert.Equal(await WrittenAsync(host, commands, abort), original);
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(actual.IsSuccess, actual.Error?.Message);
            Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(actual.Value).Fact);
        }
        Assert.Equal(1, fault.Reads);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task ShouldReplayCompletionReceiptOrRequestedCancellationGivenWinnerBeforeDerivedTotalsWriteFailure(bool abort,
        bool cancel)
    {
        // Arrange
        await using var host = new CampaignHost();
        var commands = await SeedAsync(host);
        using var cancellation = new CancellationTokenSource();
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new FaultStore(host.Content, abort, async () =>
        {
            winner = await HandleAsync(host, commands, abort, host.Content, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var actual = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            actual = await HandleAsync(host, commands, abort, fault, cancellation.Token));

        // Assert
        Assert.True(winner.IsSuccess);
        var original = Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact;
        Assert.Equal(await WrittenAsync(host, commands, abort), original);
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(actual.IsSuccess, actual.Error?.Message);
            Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(actual.Value).Fact);
        }
        Assert.Equal(1, fault.Reads);
    }

    [Theory]
    [InlineData(true)]
    public async Task ShouldReplayClosureWithoutIntentReadGivenAlreadyAcceptedNativeReceipt(bool abort)
    {
        // Arrange
        await using var host = new CampaignHost();
        var commands = await SeedAsync(host);
        var winner = await HandleAsync(host, commands, abort, host.Content, CancellationToken.None);
        Assert.True(winner.IsSuccess, winner.Error?.Message);
        var fault = new ReplayStore(host.Content, abort);

        // Act
        var replay = await HandleAsync(host, commands, abort, fault, CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact,
            Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.Equal(await WrittenAsync(host, commands, abort), Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.Equal(abort ? 0 : 1, fault.IntentReads);
        Assert.Equal(0, fault.OtherReads);
    }

    [Theory]
    [InlineData(false)]
    public async Task ShouldReplayCompletionWithOnlyRequiredSourceBindingGivenAlreadyAcceptedNativeReceipt(bool abort)
    {
        // Arrange
        await using var host = new CampaignHost();
        var commands = await SeedAsync(host);
        var winner = await HandleAsync(host, commands, abort, host.Content, CancellationToken.None);
        Assert.True(winner.IsSuccess, winner.Error?.Message);
        var fault = new ReplayStore(host.Content, abort);

        // Act
        var replay = await HandleAsync(host, commands, abort, fault, CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact,
            Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.Equal(await WrittenAsync(host, commands, abort), Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.Equal(abort ? 0 : 1, fault.IntentReads);
        Assert.Equal(0, fault.OtherReads);
    }

    [Fact]
    public async Task ShouldRejectChangedManifestGivenAcceptedCompleteReceiptWithUnchangedNativeBinding()
    {
        // Arrange
        await using var host = new CampaignHost();
        var commands = await SeedAsync(host);
        var winner = await HandleAsync(host, commands, abort: false, host.Content, CancellationToken.None);
        Assert.True(winner.IsSuccess, winner.Error?.Message);
        var changed = commands with
        {
            Complete = commands.Complete with
            { ParticipantProofManifest = commands.Complete.Intent }
        };

        // Act
        var replay = await HandleAsync(host, changed, abort: false, host.Content, CancellationToken.None);

        // Assert
        Assert.False(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, replay.Error?.Kind);
        Assert.False(replay.Error!.IsTransient);
        Assert.Equal(await WrittenAsync(host, commands, abort: false), Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task ShouldKeepPendingClosureGivenMissingOrCorruptRequiredImmutableBody(bool unavailable, bool intentBody)
    {
        // Arrange
        await using var host = new CampaignHost();
        var commands = await SeedAsync(host);
        var store = new RequiredBodyStore(host.Content,
            intentBody ? ClosureIntent.Schema : SourceUseRequirement.Schema, unavailable);

        // Act
        var result = await HandleAsync(host, commands, abort: false, store, CancellationToken.None);
        var campaign = await host.CampaignAsync(commands.Complete.CampaignId);

        // Assert
        Assert.True(store.Reads > 0);
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.NotNull(campaign.PendingClosure);
        Assert.Empty(await host.EventsAsync<PolicyCampaignClosed>(commands.Complete.Target));
        Assert.Empty(await host.EventsAsync<PolicyCampaignClosureCanceled>(commands.Complete.Target));
    }

    sealed class RequiredBodyStore(IImmutableContentStore inner, string schema, bool unavailable) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string contentSchema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, contentSchema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference.Schema != schema)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            Reads++;
            if (unavailable)
                throw new ImmutableContentUnavailableException("The required closure binding or selection bytes are unavailable.");
            throw new ImmutableContentIntegrityException("The required closure binding or selection bytes are corrupt.");
        }
    }

    static async Task<Commands> SeedAsync(CampaignHost host)
    {
        await host.SeedAsync();
        var campaignId = await host.LaunchAsync();
        var root = Uuid.CreateVersion4();
        var closed = await host.SendAsync(new CloseCampaign(root, host.Tenant, host.Program, campaignId, "Done", 1));
        Assert.True(closed.IsSuccess, closed.Error?.Message);
        var campaign = await host.CampaignAsync(campaignId);
        var accepted = campaign.PendingClosure!;
        var intent = await PolicyCampaignContent.ReadAsync<ClosureIntent>(host.Content, host.Tenant, accepted.Intent,
            CancellationToken.None);
        return new Commands(new CompletePolicyCampaignClosure(Uuid.CreateVersion4(),
                NativeFactReference.Of(campaign.Stream, accepted), campaign.Stream, root, accepted.Generation, null,
                accepted.Intent, campaignId, intent.SourceUses.Content),
            new AbortPolicyCampaignClosure(Uuid.CreateVersion4(), root, campaign.Stream, accepted.Generation,
                accepted.Revision, accepted.Intent));
    }

    static ValueTask<Result<ReceiverOutcome>> HandleAsync(CampaignHost host, Commands commands, bool abort,
        IImmutableContentStore content, CancellationToken ct)
    {
        var services = host.Scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var executor = services.GetRequiredService<IAggregateExecutor>();
        return abort
            ? new AbortPolicyCampaignClosureHandler(reader, executor, content, host.Clock, host.Events).HandleAsync(
                new Cntryl.Portia.RequestContext<AbortPolicyCampaignClosure>(commands.Abort,
                    new RequestDispatchContext(RequestActor.System)), ct)
            : new CompletePolicyCampaignClosureHandler(reader, executor, content, host.Clock,
                new PolicyCampaignClosureVerifier(reader, content, host.Events), new CampaignSource(reader, content, host.Events))
                .HandleAsync(new Cntryl.Portia.RequestContext<CompletePolicyCampaignClosure>(commands.Complete,
                    new RequestDispatchContext(RequestActor.System)), ct);
    }

    static async Task<NativeFactReference> WrittenAsync(CampaignHost host, Commands commands, bool abort) => abort
        ? NativeFactReference.Of(commands.Abort.Target,
            Assert.Single(await host.EventsAsync<PolicyCampaignClosureCanceled>(commands.Abort.Target)))
        : NativeFactReference.Of(commands.Complete.Target,
            Assert.Single(await host.EventsAsync<PolicyCampaignClosed>(commands.Complete.Target)));

    sealed record Commands(CompletePolicyCampaignClosure Complete, AbortPolicyCampaignClosure Abort);

    sealed class ReplayStore(IImmutableContentStore inner, bool abort) : IImmutableContentStore
    {
        public int IntentReads { get; private set; }
        public int OtherReads { get; private set; }

        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            throw new InvalidOperationException("An accepted closure retry cannot prepare new derived content.");

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.Schema == ClosureIntent.Schema && !abort)
            {
                IntentReads++;
                return inner.ReadVerifiedAsync(realm, reference, ct);
            }
            OtherReads++;
            throw new ImmutableContentUnavailableException("Historical closure preparation bodies are unavailable.");
        }
    }

    sealed class FaultStore(IImmutableContentStore inner, bool abort, Func<Task>? beforeFailure) : IImmutableContentStore
    {
        public int Reads { get; private set; }

        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (!abort && schema == PolicyCampaignContent.TotalsSchema)
            {
                Reads++;
                if (beforeFailure is not null)
                    await beforeFailure();
                throw new ImmutableContentUnavailableException("The losing derived closure totals write failed.");
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }

        public async Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (abort && reference.Schema == ClosureIntent.Schema)
            {
                Reads++;
                if (beforeFailure is not null)
                    await beforeFailure();
                throw new ImmutableContentUnavailableException("The actual frozen closure intent is unavailable.");
            }
            return await inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
