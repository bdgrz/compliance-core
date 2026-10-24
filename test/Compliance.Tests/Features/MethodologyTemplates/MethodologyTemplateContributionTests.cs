using Bdgrz.Compliance.Tests.Testing;
using System.Text.Json;
using Bdgrz.Compliance.Features.MethodologyTemplates;
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
    public async Task ShouldShareOnlyReviewedDeidentifiedContributionAndRetainNoClientIdentityInTemplateGivenAcceptance()
    {
        // Arrange
        await using var contribution = new MethodologyTemplateContributionScenario(Now);
        var proposedContent = new MethodologyTemplateRecordContent("quarterly-access-review", "control",
            "Review privileged access", "The owner reviews privileged access quarterly.", []);

        // Act
        var proposed = await contribution.ProposeAsync(new ProposeDeidentifiedTemplateContribution(TenantId,
            ContributionId, SourceRecordId, 3, proposedContent,
            "Removed customer name, user references, evidence and engagement identifiers."), ContributorId);
        contribution.Clock.Now = Now.AddMinutes(1);
        var selfReview = await contribution.ReviewAsync(new ReviewTemplateContribution(Uuid.CreateVersion4(),
            TenantId, ContributionId, "approved", "Ready to reuse."), ContributorId);
        contribution.Clock.Now = Now.AddMinutes(2);
        var reviewed = await contribution.ReviewAsync(new ReviewTemplateContribution(Uuid.CreateVersion4(),
            TenantId, ContributionId, "approved", "Verified no client identifiers or evidence."), ReviewerId);
        contribution.Clock.Now = Now.AddMinutes(3);
        var accepted = await contribution.AcceptAsync(new AcceptTemplateContribution(Uuid.CreateVersion4(),
            TenantId, ContributionId, "Approved for platform methodology use."), AcceptorId);
        var share = await contribution.Views.ShareAsync(TenantId, ContributionId);
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
        var contributionEvents = await contribution.FactsAsync(TenantId);
        var contributionReplay = new AggregateScenario<MethodologyTemplateContribution>(
            new MethodologyTemplateContribution(TenantId, ContributionId)).Given(contributionEvents.ToArray()).Aggregate;
        var contributionView = await contribution.Views.ReadAsync(TenantId, ContributionId);
        var events = new AggregateScenario<MethodologyTemplate>(template).PendingEvents.ToArray();
        var templateReplay = new AggregateScenario<MethodologyTemplate>(new MethodologyTemplate(TemplateId))
            .Given(events).Aggregate;

        // Assert
        Assert.True(proposed.IsSuccess, proposed.Error?.Message);
        Assert.Equal(RequestErrorKind.Forbidden, selfReview.Error?.Kind);
        Assert.True(reviewed.IsSuccess, reviewed.Error?.Message);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Null(revised);
        Assert.NotNull(share);
        Assert.Equal("accepted", contributionView!.Status);
        Assert.DoesNotContain(TenantId.ToString(), JsonSerializer.Serialize(share));
        Assert.DoesNotContain(SourceRecordId.ToString(), JsonSerializer.Serialize(share));
        Assert.DoesNotContain(TenantId.ToString(), JsonSerializer.Serialize(events));
        Assert.DoesNotContain(SourceRecordId.ToString(), JsonSerializer.Serialize(events));
        Assert.Equal("accepted", contributionReplay.Status);
        Assert.Equal(SourceRecordId, contributionView.SourceRecordId);
        Assert.Equal(template.Current.ContentSha256, templateReplay.Current!.ContentSha256);
        Assert.Equal(share.AcceptanceId, template.Current!.Versions[^1].AcceptedContributions!.Single().AcceptanceId);
        Assert.Equal(share.ContentSha256, template.Current.Versions[^1].AcceptedContributions!.Single().ContentSha256);
        Assert.Equal("quarterly-access-review", template.Current.Versions[^1].Content.Records[^1].EntryKey);
    }

    [Fact]
    public async Task ShouldRejectSourceIdentifiersAndUnreviewedAcceptanceGivenDeidentifiedContribution()
    {
        // Arrange
        await using var contribution = new MethodologyTemplateContributionScenario(Now);
        var sourceIdentifier = new MethodologyTemplateRecordContent("unsafe", "policy", "Client-specific policy",
            $"Evidence for {TenantId} was kept in {SourceRecordId}.", []);
        var rejected = await contribution.ProposeAsync(new ProposeDeidentifiedTemplateContribution(TenantId,
            ContributionId, SourceRecordId, 1, sourceIdentifier, "No client identifiers were included."), ContributorId);
        var safe = await contribution.ProposeAsync(new ProposeDeidentifiedTemplateContribution(TenantId,
            ContributionId, SourceRecordId, 1, new MethodologyTemplateRecordContent("safe", "policy", "Review policy",
                "The owner reviews the policy annually.", []), "Removed identifying details."), ContributorId);

        // Act
        contribution.Clock.Now = Now.AddMinutes(1);
        var prematureAcceptance = await contribution.AcceptAsync(new AcceptTemplateContribution(Uuid.CreateVersion4(),
            TenantId, ContributionId, "No review has occurred."), AcceptorId);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, rejected.Error?.Kind);
        Assert.True(safe.IsSuccess, safe.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, prematureAcceptance.Error?.Kind);
        Assert.Null(await contribution.Views.ShareAsync(TenantId, ContributionId));
    }
}
