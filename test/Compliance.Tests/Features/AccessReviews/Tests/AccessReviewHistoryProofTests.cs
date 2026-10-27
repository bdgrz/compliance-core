using System.Runtime.CompilerServices;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.History;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

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
