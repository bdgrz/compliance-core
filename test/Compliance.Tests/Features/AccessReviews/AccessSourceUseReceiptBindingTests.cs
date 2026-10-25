using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessSourceUseReceiptBindingTests
{
    [Theory]
    [InlineData("bulk-authorize")]
    [InlineData("bulk-settle")]
    [InlineData("completion-authorize")]
    [InlineData("completion-settle")]
    public async Task ShouldWaitForOriginalUseIntentGivenAcceptedReceiptAndUnavailableRequiredTerms(string pair)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var command = await AccessSourceUsePreparationTests.SeedAsync(fixture, pair);
        var first = await fixture.ReceiveAsync(command);
        Assert.True(first.IsSuccess, first.Error?.Message);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = await OwnerAsync(fixture, command);
        var entry = owner.Uses.Find(Key(command))!;
        var fact = pair.EndsWith("settle", StringComparison.Ordinal) ? entry.SettleFact! : entry.GrantFact!;
        var reference = Assert.IsAssignableFrom<ISourceUseFact>(fact).Intent;
        var fault = new MissingStore(services.GetRequiredService<IImmutableContentStore>(), reference);
        var before = owner.CommittedStreamPosition;

        // Act
        var result = await AccessSourceUsePreparationTests.HandleAsync(services, fault, command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
        Assert.Equal(1, fault.Reads);
        Assert.Equal(before, (await OwnerAsync(fixture, command)).CommittedStreamPosition);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldRejectChangedPinGivenAcceptedAuthorizationReceipt(bool completion, bool changeRevision)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var command = await AccessSourceUsePreparationTests.SeedAsync(fixture,
            completion ? "completion-authorize" : "bulk-authorize");
        var first = await fixture.ReceiveAsync(command);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var changed = command switch
        {
            ReserveAccessReviewItemDecision value => (IRequest<ReceiverOutcome>)(changeRevision
                ? value with { SourceRevision = value.SourceRevision + 1 }
                : value with { SourceContentEventId = Uuid.CreateVersion4() }),
            AuthorizeAccessReviewItemCampaignCompletionUse value => changeRevision
                ? value with { SourceRevision = value.SourceRevision + 1 }
                : value with { SourceContentEventId = Uuid.CreateVersion4() },
            _ => throw new ArgumentException("The request is not an authorization.", nameof(completion)),
        };

        // Act
        var refused = await fixture.ReceiveAsync(changed);

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.False(refused.Error!.IsTransient);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(first.Value).Fact.EventId,
            (await OwnerAsync(fixture, command)).Uses.Find(Key(command))!.GrantFact!.Metadata.EventId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectAnotherDispositionGivenAcceptedSettlementAndRealTerminalSource(bool completion)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var command = await AccessSourceUsePreparationTests.SeedAsync(fixture,
            completion ? "completion-settle" : "bulk-settle");
        var first = await fixture.ReceiveAsync(command);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var changed = command switch
        {
            SettleAccessReviewItemDecision value => (IRequest<ReceiverOutcome>)(value with { Disposition = SourceUseSettlementDisposition.Canceled }),
            SettleAccessReviewItemCampaignCompletionUse value => value with { Disposition = SourceUseSettlementDisposition.Canceled },
            _ => throw new ArgumentException("The request is not a settlement.", nameof(completion)),
        };

        // Act
        var refused = await fixture.ReceiveAsync(changed);

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.False(refused.Error!.IsTransient);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(first.Value).Fact.EventId,
            (await OwnerAsync(fixture, command)).Uses.Find(Key(command))!.SettleFact!.Metadata.EventId);
    }

    internal static ReservationKey Key(IRequest<ReceiverOutcome> command)
    {
        var request = command as ISourceUseAuthorization;
        var settlement = command as ISourceUseSettlement;
        var bulk = command is ReserveAccessReviewItemDecision or SettleAccessReviewItemDecision;
        return new ReservationKey(request?.OperationId ?? settlement!.OperationId, request?.Source.Stream ?? settlement!.Source.Stream,
            (ulong)AccessReviewEffects.Generation, request?.Target ?? settlement!.Target,
            SourceUseReservations.PurposeName(bulk ? SourceUsePurpose.BulkDecision : SourceUsePurpose.CampaignCompletion));
    }

    internal static async Task<AccessReviewItem> OwnerAsync(AccessReviewFixture fixture, IRequest<ReceiverOutcome> command)
    {
        var owner = command switch
        {
            ReserveAccessReviewItemDecision value => new AccessReviewItem(fixture.TenantId, value.CampaignId,
                value.SourceOwnerId, value.CommandId, value.DecisionBatchId),
            SettleAccessReviewItemDecision value => new AccessReviewItem(fixture.TenantId, value.CampaignId,
                value.SourceOwnerId, value.CommandId, value.DecisionBatchId),
            AuthorizeAccessReviewItemCampaignCompletionUse value => new AccessReviewItem(fixture.TenantId, value.CampaignId,
                value.SourceOwnerId, value.CommandId),
            SettleAccessReviewItemCampaignCompletionUse value => new AccessReviewItem(fixture.TenantId, value.CampaignId,
                value.SourceOwnerId, value.CommandId),
            _ => throw new ArgumentException("The request has no Access source-use owner.", nameof(command)),
        };
        await using var scope = fixture.Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(owner.ForSourceUses(Key(command)));
    }

    sealed class MissingStore(IImmutableContentStore inner, ImmutableContentRef selected) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference != selected)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            Reads++;
            throw new ImmutableContentUnavailableException("The required recorded use intent is unavailable.");
        }
    }
}
