using Bdgrz.Compliance.Features.AccessReviews.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessReviewReadFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReturnTypedSourceConflictGivenExactDecisionBodyUnavailableOrCorrupt(bool missing)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await AccessReviewBulkDecisionTests.LaunchedAsync(fixture);
        await fixture.SendAsync(fixture.ReviewerUserId, fixture.Decision(campaignId, selected[0], 1,
            "keep", "Current scoped decision."));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = new FailedBodyStore(services.GetRequiredService<IImmutableContentStore>(), missing);
        var reader = new AccessReviewReader(services.GetRequiredService<IAggregateReader>(), store,
            new AccessReviewManifests(store), services.GetRequiredService<IDomainEventReader>());
        var handler = ActivatorUtilities.CreateInstance<GetAccessReviewCampaignHandler>(services, reader);
        var context = new Cntryl.Portia.RequestContext<GetAccessReviewCampaign>(
            new GetAccessReviewCampaign(fixture.TenantId, campaignId),
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.ManagerUserId)));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(missing, result.Error.IsTransient);
        Assert.Equal(2, (await fixture.ItemAsync(campaignId, selected[0])).Revision);
    }

    sealed class FailedBodyStore(IImmutableContentStore inner, bool missing) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => reference.Schema == AccessReviewContent.DecisionsSchema
            ? throw (missing ? new ImmutableContentUnavailableException("The selected decision bytes are temporarily absent.")
                : new ImmutableContentIntegrityException("The selected decision bytes failed verification."))
            : inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
