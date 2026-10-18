using System.Text.Json;
using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates;

public sealed class MethodologyTemplateContributionTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ContributionId = Uuid.CreateVersion4();
    static readonly Uuid SourceRecordId = Uuid.CreateVersion4();
    static readonly Uuid TemplateId = Uuid.CreateVersion4();
    static readonly Uuid ContributorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid AcceptorId = Uuid.CreateVersion4();
    static readonly Uuid TemplateAuthorId = Uuid.CreateVersion4();
    static readonly Uuid TemplateReviewerId = Uuid.CreateVersion4();
    static readonly Uuid TemplateApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 18, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldShareOnlyReviewedDeidentifiedContributionAndRetainNoClientIdentityInTemplateGivenAcceptance()
    {
        // Arrange
        var contribution = new MethodologyTemplateContribution(TenantId, ContributionId);
        var proposedContent = new MethodologyTemplateRecordContent("quarterly-access-review", "control",
            "Review privileged access", "The owner reviews privileged access quarterly.", []);

        // Act
        var proposed = contribution.Propose(SourceRecordId, 3, proposedContent,
            "Removed customer name, user references, evidence and engagement identifiers.",
            ContributorId, "Client compliance lead", Now);
        var selfReview = contribution.Review(contribution.Revision, "approved", "Ready to reuse.",
            ContributorId, "Client compliance lead", Now.AddMinutes(1));
        var reviewed = contribution.Review(contribution.Revision, "approved",
            "Verified the proposal contains no client identifiers or evidence.",
            ReviewerId, "Independent reviewer", Now.AddMinutes(2));
        var accepted = contribution.Accept(contribution.Revision, "Approved for platform methodology use.",
            AcceptorId, "Platform operator", Now.AddMinutes(3));
        var share = contribution.ShareableContent;
        var template = new MethodologyTemplate(TemplateId);
        Assert.Null(template.Create(MethodologyTemplateTests.Content("Platform authored procedure."),
            "Initial version.", TemplateAuthorId, "Practice lead", Now));
        var content = template.Current!.Versions[^1].Content with
        {
            Records = [.. template.Current.Versions[^1].Content.Records, share!.Content],
        };
        var revised = template.ReviseDraftWithAcceptedContribution(template.Revision, content, share!,
            "Added a reviewed deidentified access-review procedure.", TemplateAuthorId,
            "Practice lead", Now.AddMinutes(4));
        var contributionEvents = new AggregateScenario<MethodologyTemplateContribution>(contribution)
            .PendingEvents.ToArray();
        var contributionReplay = new AggregateScenario<MethodologyTemplateContribution>(
            new MethodologyTemplateContribution(TenantId, ContributionId)).Given(contributionEvents).Aggregate;
        var events = new AggregateScenario<MethodologyTemplate>(template).PendingEvents.ToArray();
        var templateReplay = new AggregateScenario<MethodologyTemplate>(new MethodologyTemplate(TemplateId))
            .Given(events).Aggregate;

        // Assert
        Assert.Null(proposed);
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(selfReview).Code);
        Assert.Null(reviewed);
        Assert.Null(accepted);
        Assert.Null(revised);
        Assert.NotNull(share);
        Assert.Equal("accepted", contribution.Current!.Status);
        Assert.DoesNotContain(TenantId.ToString(), JsonSerializer.Serialize(share));
        Assert.DoesNotContain(SourceRecordId.ToString(), JsonSerializer.Serialize(share));
        Assert.DoesNotContain(TenantId.ToString(), JsonSerializer.Serialize(events));
        Assert.DoesNotContain(SourceRecordId.ToString(), JsonSerializer.Serialize(events));
        Assert.Equal(contribution.Current.ContentSha256, contributionReplay.Current!.ContentSha256);
        Assert.Equal(SourceRecordId, contributionReplay.Current.SourceRecordId);
        Assert.Equal(template.Current.ContentSha256, templateReplay.Current!.ContentSha256);
        Assert.Equal(share.AcceptanceId, template.Current!.Versions[^1].AcceptedContributions!.Single().AcceptanceId);
        Assert.Equal(share.ContentSha256, template.Current.Versions[^1].AcceptedContributions!.Single().ContentSha256);
        Assert.Equal("quarterly-access-review", template.Current.Versions[^1].Content.Records[^1].EntryKey);
    }

    [Fact]
    public void ShouldRejectSourceIdentifiersAndUnreviewedAcceptanceGivenDeidentifiedContribution()
    {
        // Arrange
        var contribution = new MethodologyTemplateContribution(TenantId, ContributionId);
        var sourceIdentifier = new MethodologyTemplateRecordContent("unsafe", "policy", "Client-specific policy",
            $"Evidence for {TenantId} was kept in {SourceRecordId}.", []);
        var rejected = contribution.Propose(SourceRecordId, 1, sourceIdentifier,
            "No client identifiers were included.", ContributorId, "Contributor", Now);
        var safe = contribution.Propose(SourceRecordId, 1,
            new MethodologyTemplateRecordContent("safe", "policy", "Review the policy",
                "The policy owner reviews the policy annually.", []),
            "Removed identifying details.", ContributorId, "Contributor", Now);

        // Act
        var prematureAcceptance = contribution.Accept(contribution.Revision,
            "No review has occurred.", AcceptorId, "Platform operator", Now.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(rejected).Code);
        Assert.Null(safe);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(prematureAcceptance).Code);
        Assert.Null(contribution.ShareableContent);
    }
}
