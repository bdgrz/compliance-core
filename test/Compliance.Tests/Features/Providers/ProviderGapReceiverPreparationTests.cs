using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderGapReceiverPreparationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayOriginalReceiverReceiptOrRequestedCancellationGivenWinnerBeforeIntentReadFailure(bool cancel)
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var context = await SeedAsync(host);
        await using var scope = host.Services.CreateAsyncScope();
        var healthy = Handler(scope.ServiceProvider, host, host.Content);
        using var cancellation = new CancellationTokenSource();
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new FaultStore(host.Content, async () =>
        {
            winner = await healthy.HandleAsync(context, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var losing = Handler(scope.ServiceProvider, host, fault);
        var result = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = await losing.HandleAsync(context, cancellation.Token));
        var frame = Assert.IsType<ProviderCoverageGapRiskAcceptanceLinked>(Assert.Single(await host.FactsAsync(context.Request.Target)));

        // Assert
        Assert.True(winner.IsSuccess);
        Assert.Equal(NativeFactReference.Of(context.Request.Target, frame), Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact);
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact,
                Assert.IsType<ReceiverOutcome.Replayed>(result.Value).Fact);
        }
        Assert.Equal(1, fault.Reads);
    }

    [Fact]
    public async Task ShouldReplayReceiverWithoutImmutablePreparationGivenAlreadyAcceptedChildReceipt()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var context = await SeedAsync(host);
        await using var scope = host.Services.CreateAsyncScope();
        var winner = await Handler(scope.ServiceProvider, host, host.Content).HandleAsync(context, CancellationToken.None);
        Assert.True(winner.IsSuccess, winner.Error?.Message);
        var fault = new FaultStore(host.Content, null);

        // Act
        var replay = await Handler(scope.ServiceProvider, host, fault).HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact,
            Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.Equal(0, fault.Reads);
        Assert.Single(await host.FactsAsync(context.Request.Target));
    }

    [Fact]
    public async Task ShouldWaitTransientlyGivenUnavailableRequiredIntentAndNoNativeWinner()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var context = await SeedAsync(host);
        await using var scope = host.Services.CreateAsyncScope();
        var fault = new FaultStore(host.Content, null);

        // Act
        var result = await Handler(scope.ServiceProvider, host, fault).HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.Empty(await host.FactsAsync(context.Request.Target));
        Assert.Equal(1, fault.Reads);
    }

    static MaterializeProviderRiskAcceptanceLinkHandler Handler(IServiceProvider services, ProviderAssuranceHost host,
        IImmutableContentStore content) => new(services.GetRequiredService<IAggregateReader>(),
        services.GetRequiredService<IAggregateExecutor>(), content, services.GetRequiredService<TimeProvider>(),
        new ProviderGapReader(services.GetRequiredService<IAggregateReader>(), host.Events),
        services.GetRequiredService<ISourceFactResolver>());

    static async Task<Cntryl.Portia.RequestContext<MaterializeProviderRiskAcceptanceLink>> SeedAsync(ProviderAssuranceHost host)
    {
        var gap = await host.RecordGapAsync();
        var risk = await host.AcceptRiskAsync();
        var linkId = Uuid.CreateVersion4();
        var admitted = await host.LinkRequestAsync(gap.GapId, 1, risk.RiskId, risk.AcceptanceId, linkId);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        var root = await host.RootAsync(ProviderAssuranceStreams.Gap(host.Tenant, host.ProviderId, gap.GapId), linkId);
        var fact = (ProviderCoverageGapRiskAcceptanceAdmitted)root.Event;
        var command = new MaterializeProviderRiskAcceptanceLink(Uuid.CreateVersion4(), NativeFactReference.Of(root.Stream, fact),
            ProviderAssuranceStreams.RiskAcceptance(host.Tenant, linkId), linkId, fact.Generation, null, fact.Intent, gap.GapId, linkId);
        return new Cntryl.Portia.RequestContext<MaterializeProviderRiskAcceptanceLink>(command,
            new RequestDispatchContext(RequestActor.System));
    }

    sealed class FaultStore(IImmutableContentStore inner, Func<Task>? beforeFailure) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);

        public async Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference.Schema == ProviderRiskLinkIntent.Schema)
            {
                Reads++;
                if (beforeFailure is not null)
                    await beforeFailure();
                throw new ImmutableContentUnavailableException("The actual captured provider risk-link intent is unavailable.");
            }
            return await inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
