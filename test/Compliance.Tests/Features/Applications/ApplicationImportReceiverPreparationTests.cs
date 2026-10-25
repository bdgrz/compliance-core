using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportReceiverPreparationTests
{
    [Theory]
    [InlineData("claim", false)]
    [InlineData("claim", true)]
    [InlineData("source-release", false)]
    [InlineData("source-release", true)]
    [InlineData("target-release", false)]
    [InlineData("target-release", true)]
    public async Task ShouldReplayReceiptOrRequestedCancellationGivenWinnerBeforeRequiredImportIntentReadFailure(
        string receiver, bool cancel)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var command = await SeedAsync(host, receiver);
        var before = (await host.EventsAsync<DomainEvent>(ApplicationImportPreparationSafetyTests.TargetOf(command))).Count;
        var healthy = Receivers(host, host.Content);
        using var cancellation = new CancellationTokenSource();
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new FaultStore(host.Content, async () =>
        {
            winner = await HandleAsync(healthy, command, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var result = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = await HandleAsync(Receivers(host, fault),
            command, cancellation.Token));

        // Assert
        Assert.True(winner.IsSuccess);
        var original = Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact;
        Assert.NotEqual(Uuid.Empty, original.EventId);
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(result.Value).Fact);
        }
        Assert.Equal(1, fault.Reads);
        Assert.Equal(before + 1, (await host.EventsAsync<DomainEvent>(ApplicationImportPreparationSafetyTests.TargetOf(command))).Count);
    }

    [Theory]
    [InlineData("claim")]
    [InlineData("source-release")]
    [InlineData("target-release")]
    public async Task ShouldReplayOriginalReceiptWithoutPlanBodyGivenExactNativeImportAcceptance(string receiver)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var command = await SeedAsync(host, receiver);
        var first = await HandleAsync(Receivers(host, host.Content), command, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var fault = new FaultStore(host.Content, null);

        // Act
        var replay = await HandleAsync(Receivers(host, fault), command, CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(first.Value).Fact,
            Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.Equal(0, fault.Reads);
    }

    internal static ApplicationImportReceivers Receivers(ApplicationImportHost host, IImmutableContentStore content) =>
        new(host.Reader, host.Services.GetRequiredService<IAggregateExecutor>(), content,
            new ApplicationImportVerifier(host.Reader, content), host.Activity, host.Clock, host.Events, host.Impacts);

    internal static ValueTask<Result<ReceiverOutcome>> HandleAsync(ApplicationImportReceivers receivers,
        IRequest<ReceiverOutcome> command, CancellationToken ct) => command switch
        {
            ClaimApplicationImportSource value => receivers.ClaimAsync(Context(value), value, ct),
            ReleaseApplicationImportSource value => receivers.ReleaseSourceAsync(Context(value), value, ct),
            ReleaseApplicationImportTarget value => receivers.ReleaseTargetAsync(Context(value), value, ct),
            _ => throw new ArgumentException("The receiver is not supported by this fixture.", nameof(command)),
        };

    internal static Cntryl.Portia.RequestContext<T> Context<T>(T command) where T : IRequestBase =>
        new(command, new RequestDispatchContext(RequestActor.System));

    internal static async Task<IRequest<ReceiverOutcome>> SeedAsync(ApplicationImportHost host, string receiver)
    {
        var (batch, process, applicationId) = await SealedAsync(host);
        var sourceTarget = process.Targets[0];
        var claim = (await host.StepAsync(process, "claim", nameof(ClaimApplicationImportSource), sourceTarget))!;
        if (receiver == "claim")
            return claim;
        var claimed = await host.SendAsync(claim);
        Assert.True(claimed.IsSuccess, claimed.Error?.Message);
        var target = ApplicationImportEffects.TargetKey(applicationId);
        var reserved = await host.SendAsync((await host.StepAsync(process, "reserve", nameof(ReserveApplicationImportTarget), target))!);
        Assert.True(reserved.IsSuccess, reserved.Error?.Message);
        var applied = await host.SendAsync((await host.StepAsync(process, "apply", nameof(ApplyApplicationImportEffect), target))!);
        Assert.True(applied.IsSuccess, applied.Error?.Message);
        var committed = await host.SendAsync((await host.StepAsync(process, "publish", nameof(CommitApplicationImportBatch)))!);
        Assert.True(committed.IsSuccess, committed.Error?.Message);
        Assert.NotNull((await host.BatchAsync(batch.Id)).Committed);
        return receiver == "source-release"
            ? (await host.StepAsync(process, "settle", nameof(ReleaseApplicationImportSource), sourceTarget))!
            : (await host.StepAsync(process, "settle", nameof(ReleaseApplicationImportTarget), target))!;
    }

    internal static async Task<(ApplicationImportBatch Batch, ProcessRecord Process, Uuid ApplicationId)> SealedAsync(
        ApplicationImportHost host)
    {
        var batch = await host.StageBatchAsync();
        var applicationId = Uuid.CreateVersion4();
        var sealedBatch = await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(applicationId, "record-a")]);
        var process = await host.StartProcessAsync(batch.Id);
        return (sealedBatch, process, applicationId);
    }

    internal sealed class FaultStore(IImmutableContentStore inner, Func<Task>? beforeFailure) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public async Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference.Schema == ImportFrozenPlanIntent.Schema)
            {
                Reads++;
                if (beforeFailure is not null)
                    await beforeFailure();
                throw new ImmutableContentUnavailableException("The actual frozen import intent is unavailable.");
            }
            return await inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
