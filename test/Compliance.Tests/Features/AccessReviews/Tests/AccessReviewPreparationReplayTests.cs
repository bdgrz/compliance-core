using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

public sealed class AccessReviewPreparationReplayTests
{
    [Fact]
    public async Task ShouldReplayEqualDecisionWinnerGivenLosingManifestFreezeUnavailableAfterConcurrentAdmission()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await AccessReviewBulkDecisionTests.LaunchedAsync(fixture);
        var command = fixture.Decision(campaignId, selected[0], 1, "keep", "The original personal decision.");
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        AccessDecisionView? winner = null;
        var store = new LosingStore(services.GetRequiredService<IImmutableContentStore>(), async () =>
            winner = await fixture.SendAsync(fixture.ReviewerUserId, command));
        var native = new NativeOperationExecutor(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<IAggregateExecutor>(), store);
        var handler = ActivatorUtilities.CreateInstance<RecordAccessDecisionHandler>(services, native, store);
        var context = new Cntryl.Portia.RequestContext<RecordAccessDecision>(command,
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.ReviewerUserId),
                new HttpInvocation("PUT", "/access-review/decision", "/access-review/decision", "replay-test")));
        Result<AccessDecisionView> replay = default;

        // Act
        var error = await Record.ExceptionAsync(async () => replay = await handler.HandleAsync(context, CancellationToken.None));
        var saved = await fixture.ItemAsync(campaignId, selected[0]);

        // Assert
        Assert.Null(error);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(winner, replay.Value);
        Assert.Equal(2, saved.Revision);
        Assert.Equal(1, store.CompetingAdmissions);
    }

    sealed class LosingStore(IImmutableContentStore inner, Func<Task> admit) : IImmutableContentStore
    {
        bool _armed = true;
        public int CompetingAdmissions { get; private set; }
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (_armed && schema == AccessReviewContent.DecisionsSchema)
            {
                _armed = false;
                await admit();
                CompetingAdmissions++;
                throw new ImmutableContentUnavailableException("The losing decision manifest write was unavailable.");
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
