using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessReviewHistoryProofTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldRejectCoherentUnselectedMiddleSubstitutionGivenActualHydratedNativePrefix(
        bool campaignHistory, bool metadataOnly)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await AccessReviewBulkDecisionTests.LaunchedAsync(fixture);
        Aggregate owner;
        if (campaignHistory)
        {
            await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
            await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
            owner = await fixture.CampaignAggregateAsync(campaignId);
        }
        else
        {
            await fixture.SendAsync(fixture.ReviewerUserId, fixture.Decision(campaignId, selected[0], 1,
                "keep", "First independent review."));
            await fixture.SendAsync(fixture.ReviewerUserId, fixture.Decision(campaignId, selected[0], 2,
                "keep", "Second independent review."));
            owner = await fixture.ItemAsync(campaignId, selected[0]);
        }
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var facts = new List<DomainEvent>();
        await foreach (var record in events.ReadAsync(owner.Stream, 0))
            facts.Add(record.Event);
        DomainEvent changed = facts[1] switch
        {
            AccessReviewDecisionBatchAdmitted admission => admission with
            {
                Intent = metadataOnly ? admission.Intent : admission.Intent with { ContentId = Uuid.CreateVersion4() },
            },
            AccessDecisionsRecorded decision => decision with
            {
                DecisionsManifest = metadataOnly ? decision.DecisionsManifest :
                    decision.DecisionsManifest with { ContentId = Uuid.CreateVersion4() },
            },
            _ => throw new InvalidOperationException("The fixture did not create the intended middle fact."),
        };
        changed.AttachMetadata(metadataOnly ? facts[1].Metadata with { EventId = Uuid.CreateVersion4() } : facts[1].Metadata);
        facts[1] = changed;
        var source = new Records(owner.Stream, facts.ToArray());

        // Act
        var error = await Record.ExceptionAsync(async () =>
        {
            if (owner is AccessReviewCampaign campaign)
                _ = await AccessReviewCampaignHistory.ReadAsync(source, campaign);
            else
                _ = await AccessReviewItemHistory.ReadAsync(source, (AccessReviewItem)owner);
        });

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(error);
        Assert.Equal(3UL, owner.Version);
    }

    [Theory]
    [InlineData("count")]
    [InlineData("order")]
    [InlineData("digest")]
    [InlineData("schema")]
    public async Task ShouldVerifyEveryManifestDescriptorGivenSameBytesAlreadyCached(string changedField)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await AccessReviewBulkDecisionTests.LaunchedAsync(fixture);
        var (request, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        var campaign = await fixture.CampaignAggregateAsync(campaignId, selectedRootId: request.DecisionBatchId);
        var content = fixture.Provider.GetRequiredService<IImmutableContentStore>();
        var intent = await AccessReviewContent.ReadAsync<BulkAccessDecisionIntent>(content, fixture.TenantId,
            campaign.AdmittedFor(request.DecisionBatchId)!.Intent, CancellationToken.None);
        var manifests = new AccessReviewManifests(content);
        var realm = fixture.TenantId.ToString();
        var original = await manifests.BulkItemsAsync(realm, intent.Items);
        var changed = changedField switch
        {
            "count" => intent.Items with { Count = intent.Items.Count - 1 },
            "order" => intent.Items with { Order = SelectionOrder.Authored },
            "digest" => intent.Items with { ItemsSha256 = new string('0', 64) },
            _ => intent.Items with { ItemSchema = "another-item-schema" },
        };

        // Act
        var error = await Record.ExceptionAsync(async () => _ = await manifests.BulkItemsAsync(realm, changed));

        // Assert
        Assert.Equal(selected.Count, original.Count);
        Assert.IsType<ImmutableContentIntegrityException>(error);
    }

    [Fact]
    public async Task ShouldKeepOriginalSelectedPublicationAndAdmissionGivenLaterIndependentBulkDecision()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (campaignId, selected) = await AccessReviewBulkDecisionTests.LaunchedAsync(fixture);
        var (first, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        await fixture.RunBulkAsync(campaignId, first.DecisionBatchId);
        var (second, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
        await fixture.RunBulkAsync(campaignId, second.DecisionBatchId);

        // Act
        var campaign = await fixture.CampaignAggregateAsync(campaignId, selectedRootId: first.DecisionBatchId);
        var item = await fixture.ItemAsync(campaignId, selected[0], selectedBatchId: first.DecisionBatchId);
        var history = await fixture.ItemHistoryAsync(item);

        // Assert
        Assert.NotNull(campaign.AdmittedFor(first.DecisionBatchId));
        Assert.Null(campaign.AdmittedFor(second.DecisionBatchId));
        Assert.True(item.IsPublished(first.DecisionBatchId));
        Assert.NotNull(item.ProvisionalFor(first.DecisionBatchId));
        Assert.False(item.IsPublished(second.DecisionBatchId));
        Assert.True(history.IsPublished(first.DecisionBatchId));
        Assert.True(history.IsPublished(second.DecisionBatchId));
        Assert.Equal(3, item.Revision);
    }

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset,
            CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct = default) => Enumerate(stream, 0, ct);
        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (address != stream)
                yield break;
            for (var index = fromOffset; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], index, EventCursor.Start);
            }
        }
    }
}
