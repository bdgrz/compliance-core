using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using System.Security.Cryptography;
using System.Text.Json;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates;

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
        var application = new TemplateApplication(TenantId, ProgramId, ApplicationId);
        var version = PublishedTemplateVersion();
        Assert.Null(application.Apply(version, "apply-2026-10", ActorId, "Original display", Now));
        var draft = application.Current!.Drafts[0];
        Assert.Null(application.RecordDraftCreated(application.Revision, draft.EntryKey,
            draft.PlannedRecordId, 1, Hash('a'), "draft", ActorId, "Worker", Now.AddMinutes(1)));
        var facts = new AggregateScenario<TemplateApplication>(application).PendingEvents.ToArray();
        var replay = new AggregateScenario<TemplateApplication>(new TemplateApplication(TenantId, ProgramId,
            ApplicationId)).Given(facts).Aggregate;

        // Act
        var result = replay.Apply(version, "apply-2026-10", ActorId, "Renamed member", Now.AddHours(1));
        var changedActor = replay.Apply(version, "apply-2026-10", Uuid.CreateVersion4(), "Other", Now.AddHours(1));

        // Assert
        Assert.Null(result);
        Assert.NotNull(changedActor);
        Assert.Empty(new AggregateScenario<TemplateApplication>(replay).PendingEvents);
        Assert.Equal(Now, replay.Current!.RequestedAt);
    }

    [Fact]
    public void ShouldPinOneApprovedVersionAndCreateOnlyDraftProvenanceGivenApplication()
    {
        // Arrange
        var application = new TemplateApplication(TenantId, ProgramId, ApplicationId);
        var version = PublishedTemplateVersion();

        // Act
        var applied = application.Apply(version, "apply-2026-10", ActorId, "Client administrator", Now);
        var firstDraft = application.Current!.Drafts[0];
        var created = application.RecordDraftCreated(application.Revision, firstDraft.EntryKey,
            firstDraft.PlannedRecordId, 1, Hash('a'), "draft", ActorId, "Application worker", Now.AddMinutes(1));
        var events = new AggregateScenario<TemplateApplication>(application).PendingEvents.ToArray();
        var replay = new AggregateScenario<TemplateApplication>(
            new TemplateApplication(TenantId, ProgramId, ApplicationId)).Given(events).Aggregate;

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
        var application = new TemplateApplication(TenantId, ProgramId, ApplicationId);
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
