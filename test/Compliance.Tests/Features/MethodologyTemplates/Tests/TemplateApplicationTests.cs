using Cntryl.Portia;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.MethodologyTemplates.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class TemplateApplicationTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ApplicationId = Uuid.CreateVersion4();
    static readonly Uuid TemplateId = Uuid.CreateVersion4();
    static readonly Uuid ActorId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 18, 13, 0, 0, TimeSpan.Zero);
    static readonly string[] ExpectedDispositions = ["adapt", "decline"];
    static readonly string[] ExpectedProposedDispositions = ["adapt", "decline"];

    [Fact]
    public void ShouldReplayApplicationAfterDraftReceiptGivenLaterClockAndDisplay()
    {
        // Arrange
        var application = new TemplateApplicationRules(TenantId, ProgramId, ApplicationId);
        var version = PublishedTemplateVersion();
        Assert.Null(application.Apply(version, "apply-2026-10", ActorId, "Original display", Now));
        var draft = application.Current!.Drafts[0];
        Assert.Null(application.RecordDraftCreated(application.Revision, draft.EntryKey,
            draft.PlannedRecordId, 1, Hash('a'), "draft", ActorId, "Worker", Now.AddMinutes(1)));
        var facts = application.PendingChanges.ToArray();
        var replay = Replay(facts, (ActorId, "Original display", Now), (ActorId, "Worker", Now.AddMinutes(1)));

        // Act
        var result = replay.Apply(version, "apply-2026-10", ActorId, "Renamed member", Now.AddHours(1));
        var changedActor = replay.Apply(version, "apply-2026-10", Uuid.CreateVersion4(), "Other", Now.AddHours(1));

        // Assert
        Assert.Null(result);
        Assert.NotNull(changedActor);
        Assert.Empty(replay.PendingChanges);
        Assert.Equal(Now, replay.Current!.RequestedAt);
    }

    [Fact]
    public void ShouldPinOneApprovedVersionAndCreateOnlyDraftProvenanceGivenApplication()
    {
        // Arrange
        var application = new TemplateApplicationRules(TenantId, ProgramId, ApplicationId);
        var version = PublishedTemplateVersion();

        // Act
        var applied = application.Apply(version, "apply-2026-10", ActorId, "Client administrator", Now);
        var firstDraft = application.Current!.Drafts[0];
        var created = application.RecordDraftCreated(application.Revision, firstDraft.EntryKey,
            firstDraft.PlannedRecordId, 1, Hash('a'), "draft", ActorId, "Application worker", Now.AddMinutes(1));
        var events = application.PendingChanges.ToArray();
        var replay = Replay(events, (ActorId, "Client administrator", Now), (ActorId, "Application worker", Now.AddMinutes(1)));

        // Assert
        Assert.Null(applied);
        Assert.Null(created);
        Assert.Equal(TemplateId, application.Current.TemplateId);
        Assert.Equal(version.VersionId, application.Current.TemplateVersionId);
        Assert.Equal(version.ContentSha256, application.Current.TemplateContentSha256);
        Assert.Equal("draft_created", application.Current.Drafts[0].Status);
        Assert.Equal("draft_pending", application.Current.Drafts[1].Status);
        Assert.False(application.Current.IsActivated);
        Assert.Equal(application.Current.ContentSha256, replay.Current!.ContentSha256);
        Assert.Equal(TenantId, replay.Current.TenantId);
    }

    [Fact]
    public void ShouldRequireExactPublishedSuccessorAndRetainTenantDispositionGivenTemplateUpdate()
    {
        // Arrange
        var application = new TemplateApplicationRules(TenantId, ProgramId, ApplicationId);
        Assert.Null(application.Apply(PublishedTemplateVersion(), "apply-1", ActorId,
            "Client administrator", Now));
        var unpublished = PublishedTemplateVersion(2) with { Status = "draft" };
        var approvedSuccessor = PublishedTemplateVersion(2);
        var secondSuccessor = PublishedTemplateVersion(3);

        // Act
        var rejected = application.ProposeSuccessor(application.Revision, unpublished, "adopt",
            ActorId, "Client administrator", Now.AddMinutes(1));
        var proposed = application.ProposeSuccessor(application.Revision, approvedSuccessor, "adapt",
            ActorId, "Client administrator", Now.AddMinutes(2));
        var proposalRetry = application.ProposeSuccessor(application.Revision - 1, approvedSuccessor, "adapt",
            ActorId, "Client administrator", Now.AddMinutes(2));
        var disposition = application.RecordSuccessorDisposition(application.Revision, "adapt",
            "Map the updated review cadence to our current quarterly process.", ActorId,
            "Client administrator", Now.AddMinutes(3));
        var dispositionRetry = application.RecordSuccessorDisposition(application.Revision - 1, "adapt",
            "Map the updated review cadence to our current quarterly process.", ActorId,
            "Client administrator", Now.AddMinutes(3));
        var nextProposal = application.ProposeSuccessor(application.Revision, secondSuccessor, "decline",
            ActorId, "Client administrator", Now.AddMinutes(4));
        var nextDisposition = application.RecordSuccessorDisposition(application.Revision, "decline",
            "The successor no longer fits our operating model.", ActorId,
            "Client administrator", Now.AddMinutes(5));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(rejected).Code);
        Assert.Null(proposed);
        Assert.Null(proposalRetry);
        Assert.Null(disposition);
        Assert.Null(dispositionRetry);
        Assert.Null(nextProposal);
        Assert.Null(nextDisposition);
        Assert.Equal(2, application.Current!.SuccessorProposals.Count);
        Assert.Equal(ExpectedProposedDispositions,
            application.Current.SuccessorProposals.Select(item => item.ProposedDisposition));
        Assert.Equal(ExpectedDispositions, application.Current.SuccessorDecisions.Select(item => item.Disposition));
        Assert.Equal("draft_pending", application.Current.Drafts[0].Status);
        Assert.False(application.Current.IsActivated);
    }

    [Fact]
    public void ShouldContinueSuccessorDecisionsGivenMoreThanFormerLifetimeLimit()
    {
        // Arrange
        var application = new TemplateApplicationRules(TenantId, ProgramId, ApplicationId);
        Assert.Null(application.Apply(PublishedTemplateVersion(), "long-lived", ActorId, "Client administrator", Now));

        // Act
        for (var version = 2; version <= 71; version++)
        {
            Assert.Null(application.ProposeSuccessor(application.Revision, PublishedTemplateVersion(version),
                "adapt", ActorId, "Client administrator", Now.AddMinutes(version * 2)));
            Assert.Null(application.RecordSuccessorDisposition(application.Revision, "adapt", "Keep our exact local procedure.",
                ActorId, "Client administrator", Now.AddMinutes(version * 2 + 1)));
        }

        // Assert
        Assert.Equal(70, application.Current!.SuccessorDecisions.Count);
        Assert.Equal(70, application.Current.SuccessorProposals.Count);
        Assert.Equal(141, application.Revision);
        Assert.Equal(1, application.Current.TemplateVersionNumber);
    }

    [Fact]
    public void ShouldApplyFullLegalPublishedContentGivenBodyLargerThanNativeFrame()
    {
        // Arrange
        var application = new TemplateApplicationRules(TenantId, ProgramId, ApplicationId);
        var original = PublishedTemplateVersion();
        var content = original.Content with
        {
            Records = Enumerable.Range(0, 100).Select(index => new MethodologyTemplateRecordContent(
                $"control-{index:000}", "control", $"Control {index}", new string('x', 2000), [])).ToArray(),
        };
        var version = original with { Content = content, ContentSha256 = Digest(content) };

        // Act
        var applied = application.Apply(version, "complete-content", ActorId, "Client administrator", Now);

        // Assert
        Assert.Null(applied);
        Assert.Equal(100, application.Current!.SourceVersion.Content.Records.Count);
        Assert.Equal(version.ContentSha256, application.Current.TemplateContentSha256);
        Assert.All(application.Current.Drafts, draft => Assert.Equal("draft_pending", draft.Status));
    }

    static TemplateApplicationRules Replay(TemplateApplicationMutationContent[] changes,
        params (Uuid Actor, string Display, DateTimeOffset At)[] attribution)
    {
        var rules = new TemplateApplicationRules(TenantId, ProgramId, ApplicationId);
        for (var index = 0; index < changes.Length; index++)
            rules.ApplyChange(changes[index], index + 1, attribution[index].Actor, attribution[index].Display, attribution[index].At);
        return rules;
    }

    static MethodologyTemplateVersionView PublishedTemplateVersion(long versionNumber = 1)
    {
        var first = MethodologyTemplateTests.Content("Original procedure.");
        var content = first with
        {
            Records =
            [
                new("annual-policy-review", "policy", "Review the policy annually",
                    "The policy owner reviews and approves the current policy at least annually.", []),
                .. first.Records,
            ],
        };
        return new MethodologyTemplateVersionView(TemplateId, Uuid.CreateVersion4(), versionNumber,
            "published", content, Digest(content), Uuid.CreateVersion4(), "Practice lead", Now,
            new MethodologyTemplateReviewView(Uuid.CreateVersion4(), "approved", "Reviewed.",
                Uuid.CreateVersion4(), "Independent reviewer", Now.AddMinutes(1)),
            Uuid.CreateVersion4(), "Platform approver", Now.AddMinutes(2), "Initial version.", [], "Approved for reuse.");
    }

    static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    static string Hash(char value) => new(value, 64);
}
