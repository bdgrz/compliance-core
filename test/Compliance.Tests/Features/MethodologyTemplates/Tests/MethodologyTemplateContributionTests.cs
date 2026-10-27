using Bdgrz.Compliance.Tests.Testing;
using System.Text.Json;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
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
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

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
        await using var template = new MethodologyTemplateScenario(Now);
        Assert.True((await template.CreateAsync(new CreateMethodologyTemplate(TemplateId,
            MethodologyTemplateTests.Content("Platform authored procedure."), "Initial version."), TemplateAuthorId)).IsSuccess);
        var initial = await template.Views.ReadAsync(TemplateId);
        var content = initial!.Versions[^1].Content with
        {
            Records = [.. initial.Versions[^1].Content.Records, share!.Content],
        };
        template.Clock.Now = Now.AddMinutes(4);
        var request = new ReviseMethodologyTemplateDraft(Uuid.CreateVersion4(), TemplateId, initial.Revision,
            content, "Added a reviewed deidentified access-review procedure.");
        // Exercise the native local boundary with already verified fixture share material. The public cross-tenant
        // transfer producer remains explicitly blocked until its selected exact proof contract is supplied.
        var revised = await template.Operations().ExecuteAsync(MethodologyTemplateScenario.Context(request,
            TemplateAuthorId), TemplateId, request.CommandId, nameof(ReviseMethodologyTemplateDraft),
            request.ExpectedRevision, (owner, member) => owner.PrepareRevise(request.ExpectedRevision),
            (rules, member, display, at) => rules.ReviseDraftWithAcceptedContribution(request.ExpectedRevision,
                request.Content, share!, request.ChangeNotes, member, display, at), CancellationToken.None);
        var current = await template.Views.ReadAsync(TemplateId);
        var contributionEvents = await contribution.FactsAsync(TenantId);
        var contributionReplay = new AggregateScenario<MethodologyTemplateContribution>(
            new MethodologyTemplateContribution(TenantId, ContributionId)).Given(contributionEvents.ToArray()).Aggregate;
        var contributionView = await contribution.Views.ReadAsync(TenantId, ContributionId);
        var events = await template.FactsAsync(TemplateId);
        var templateReplay = new AggregateScenario<MethodologyTemplate>(new MethodologyTemplate(TemplateId))
            .Given(events.ToArray()).Aggregate;
        var replayView = await template.Views.ReadAsync(templateReplay, CancellationToken.None);

        // Assert
        Assert.True(proposed.IsSuccess, proposed.Error?.Message);
        Assert.Equal(RequestErrorKind.Forbidden, selfReview.Error?.Kind);
        Assert.True(reviewed.IsSuccess, reviewed.Error?.Message);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        Assert.NotNull(share);
        Assert.Equal("accepted", contributionView!.Status);
        Assert.DoesNotContain(TenantId.ToString(), JsonSerializer.Serialize(share));
        Assert.DoesNotContain(SourceRecordId.ToString(), JsonSerializer.Serialize(share));
        Assert.DoesNotContain(TenantId.ToString(), JsonSerializer.Serialize(events));
        Assert.DoesNotContain(SourceRecordId.ToString(), JsonSerializer.Serialize(events));
        Assert.Equal("accepted", contributionReplay.Status);
        Assert.Equal(SourceRecordId, contributionView.SourceRecordId);
        Assert.Equal(current!.ContentSha256, replayView!.ContentSha256);
        Assert.Equal(share.AcceptanceId, current!.Versions[^1].AcceptedContributions!.Single().AcceptanceId);
        Assert.Equal(share.ContentSha256, current.Versions[^1].AcceptedContributions!.Single().ContentSha256);
        Assert.Equal("quarterly-access-review", current.Versions[^1].Content.Records[^1].EntryKey);
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
