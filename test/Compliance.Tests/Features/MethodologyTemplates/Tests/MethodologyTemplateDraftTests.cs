using System.Text.Json;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateDraftTests
{
    static readonly Uuid TemplateId = Uuid.CreateVersion4();
    static readonly Uuid DraftId = Uuid.CreateVersion4();
    static readonly Uuid FirstAuthor = Uuid.CreateVersion4();
    static readonly Uuid SecondAuthor = Uuid.CreateVersion4();
    static readonly Uuid Reviewer = Uuid.CreateVersion4();
    static readonly Uuid Approver = Uuid.CreateVersion4();

    [Fact]
    public void ShouldExcludeEveryRevisionAuthorGivenDraftAuthorshipChanges()
    {
        // Arrange
        var scenario = Opened();
        Assert.True(Revise(scenario.Aggregate, 0, "First", FirstAuthor).IsSuccess);
        Assert.True(Revise(scenario.Aggregate, 1, "Second", SecondAuthor).IsSuccess);
        var count = scenario.PendingEvents.Count;

        // Act
        var originalAuthor = scenario.Aggregate.Review(2, FirstAuthor, true, "Accept");
        var latestAuthor = scenario.Aggregate.Review(2, SecondAuthor, true, "Accept");
        var independent = scenario.Aggregate.Review(2, Reviewer, true, "Accept");

        // Assert
        Assert.False(originalAuthor.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, originalAuthor.Error.Kind);
        Assert.False(latestAuthor.IsSuccess);
        Assert.True(independent.IsSuccess);
        Assert.Equal(count + 1, scenario.PendingEvents.Count);
        Assert.Equal("reviewed", scenario.Aggregate.Status);
        Assert.Equal(Reviewer, scenario.Aggregate.AcceptedReviewer);
        Assert.True(new[] { FirstAuthor, SecondAuthor }.ToHashSet().SetEquals(scenario.Aggregate.RevisionAuthors));
    }

    [Fact]
    public void ShouldRequireFreshReviewGivenAcceptedDraftIsRevised()
    {
        // Arrange
        var scenario = Opened();
        Assert.True(Revise(scenario.Aggregate, 0, "First", FirstAuthor).IsSuccess);
        Assert.True(scenario.Aggregate.Review(1, Reviewer, true, "Accept").IsSuccess);

        // Act
        var revised = Revise(scenario.Aggregate, 1, "Second", SecondAuthor);
        var statusAfterRevision = scenario.Aggregate.Status;
        var reviewerAfterRevision = scenario.Aggregate.AcceptedReviewer;
        var premature = scenario.Aggregate.Approve(2, Approver);
        var reviewed = scenario.Aggregate.Review(2, Reviewer, true, "Rechecked");
        var approved = scenario.Aggregate.Approve(2, Approver);

        // Assert
        Assert.True(revised.IsSuccess);
        Assert.Equal("drafting", statusAfterRevision);
        Assert.Null(reviewerAfterRevision);
        Assert.False(premature.IsSuccess);
        Assert.True(reviewed.IsSuccess);
        Assert.True(approved.IsSuccess);
        Assert.Equal("approved", scenario.Aggregate.Status);
    }

    [Theory]
    [InlineData("first_author")]
    [InlineData("second_author")]
    [InlineData("reviewer")]
    [InlineData("third_person")]
    public void ShouldApproveOnlyByIndependentThirdPersonGivenAcceptedCurrentRevision(string actor)
    {
        // Arrange
        var scenario = Opened();
        Assert.True(Revise(scenario.Aggregate, 0, "First", FirstAuthor).IsSuccess);
        Assert.True(Revise(scenario.Aggregate, 1, "Second", SecondAuthor).IsSuccess);
        Assert.True(scenario.Aggregate.Review(2, Reviewer, true, "Accept").IsSuccess);
        var approver = actor switch
        {
            "first_author" => FirstAuthor,
            "second_author" => SecondAuthor,
            "reviewer" => Reviewer,
            _ => Approver,
        };
        var count = scenario.PendingEvents.Count;

        // Act
        var result = scenario.Aggregate.Approve(2, approver);

        // Assert
        Assert.Equal(actor == "third_person", result.IsSuccess);
        Assert.Equal(count + (result.IsSuccess ? 1 : 0), scenario.PendingEvents.Count);
        if (!result.IsSuccess)
            Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public void ShouldRefuseStaleReviewWithoutFactGivenCurrentRevisionAdvanced()
    {
        // Arrange
        var scenario = Opened();
        Assert.True(Revise(scenario.Aggregate, 0, "First", FirstAuthor).IsSuccess);
        var count = scenario.PendingEvents.Count;

        // Act
        var stale = scenario.Aggregate.Review(0, Reviewer, true, "Accept");
        var returned = scenario.Aggregate.Review(1, Reviewer, false, "Explain the control");

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.True(returned.IsSuccess);
        Assert.Equal(count + 1, scenario.PendingEvents.Count);
        Assert.Equal("drafting", scenario.Aggregate.Status);
        Assert.Null(scenario.Aggregate.AcceptedReviewer);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldKeepExactTerminalRetryFactFreeAndRefuseLaterChangesGivenApprovedOrDiscardedDraft(bool discard)
    {
        // Arrange
        var scenario = Opened();
        Assert.True(Revise(scenario.Aggregate, 0, "First", FirstAuthor).IsSuccess);
        if (discard)
            Assert.True(scenario.Aggregate.Discard(FirstAuthor, "No longer needed").IsSuccess);
        else
        {
            Assert.True(scenario.Aggregate.Review(1, Reviewer, true, "Accept").IsSuccess);
            Assert.True(scenario.Aggregate.Approve(1, Approver).IsSuccess);
        }
        var count = scenario.PendingEvents.Count;

        // Act
        var retry = discard ? scenario.Aggregate.Discard(FirstAuthor, "No longer needed")
            : scenario.Aggregate.Approve(1, Approver);
        var revised = Revise(scenario.Aggregate, 1, "Later", SecondAuthor);
        var reviewed = scenario.Aggregate.Review(1, Reviewer, true, "Another review");
        var changedTerminal = discard ? scenario.Aggregate.Discard(FirstAuthor, "Changed reason")
            : scenario.Aggregate.Approve(1, Uuid.CreateVersion4());

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(revised.IsSuccess);
        Assert.False(reviewed.IsSuccess);
        Assert.False(changedTerminal.IsSuccess);
        Assert.Equal(count, scenario.PendingEvents.Count);
    }

    [Theory]
    [InlineData("duplicate_key")]
    [InlineData("contribution")]
    [InlineData("unsupported_kind")]
    [InlineData("digest")]
    public void ShouldRefuseWithoutFactGivenInvalidRevisionContent(string fault)
    {
        // Arrange
        var scenario = Opened();
        var entry = Entry("First");
        IReadOnlyList<MethodologyTemplateRecordContent> entries = fault switch
        {
            "duplicate_key" => [entry, entry],
            "contribution" => [entry with { ContributionApprovalId = Uuid.CreateVersion4() }],
            "unsupported_kind" => [entry with { RecordKind = "evidence_request" }],
            _ => [entry],
        };
        var content = Content(entries);
        if (fault == "digest")
            content = content with { Sha256 = new string('a', 64) };
        var count = scenario.PendingEvents.Count;

        // Act
        var result = scenario.Aggregate.Revise(0, entries, content, FirstAuthor);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(count, scenario.PendingEvents.Count);
        Assert.Equal(0, scenario.Aggregate.Revision);
    }

    [Fact]
    public void ShouldRequireReasonWithoutFactGivenDiscardRequested()
    {
        // Arrange
        var scenario = Opened();
        var count = scenario.PendingEvents.Count;

        // Act
        var result = scenario.Aggregate.Discard(FirstAuthor, " ");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(count, scenario.PendingEvents.Count);
        Assert.Equal("drafting", scenario.Aggregate.Status);
    }

    [Fact]
    public void ShouldPreserveAuthorsAndAcceptanceGivenRetainedDraftFacts()
    {
        // Arrange
        var scenario = Opened();
        Assert.True(Revise(scenario.Aggregate, 0, "First", FirstAuthor).IsSuccess);
        Assert.True(Revise(scenario.Aggregate, 1, "Second", SecondAuthor).IsSuccess);
        Assert.True(scenario.Aggregate.Review(2, Reviewer, true, "Accept").IsSuccess);
        var replay = new AggregateScenario<MethodologyTemplateDraft>(new MethodologyTemplateDraft(TemplateId, DraftId))
            .Given(scenario.PendingEvents.ToArray());

        // Act
        var selfApproval = replay.Aggregate.Approve(2, FirstAuthor);
        var approved = replay.Aggregate.Approve(2, Approver);

        // Assert
        Assert.False(selfApproval.IsSuccess);
        Assert.True(approved.IsSuccess);
        Assert.Equal(2, replay.Aggregate.RevisionAuthors.Count);
        Assert.Equal(scenario.Aggregate.EntriesContent, replay.Aggregate.EntriesContent);
        Assert.Equal("approved", replay.Aggregate.Status);
    }

    [Fact]
    public void ShouldKeepOriginalOpeningRetryFactFreeGivenDraftAlreadyRevised()
    {
        // Arrange
        var scenario = new AggregateScenario<MethodologyTemplateDraft>(new MethodologyTemplateDraft(TemplateId, DraftId));
        var source = Uuid.CreateVersion4();
        var initial = Content([]);
        Assert.True(scenario.Aggregate.Open(source, 0, [], initial).IsSuccess);
        Assert.True(Revise(scenario.Aggregate, 0, "First", FirstAuthor).IsSuccess);
        var count = scenario.PendingEvents.Count;

        // Act
        var repeated = scenario.Aggregate.Open(source, 0, [], initial);
        var changed = scenario.Aggregate.Open(Uuid.CreateVersion4(), 0, [], initial);

        // Assert
        Assert.True(repeated.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(count, scenario.PendingEvents.Count);
        Assert.Equal(1, scenario.Aggregate.Revision);
    }

    [Fact]
    public void ShouldPreserveAcceptanceGivenExactRevisionAndReviewRetries()
    {
        // Arrange
        var scenario = Opened();
        IReadOnlyList<MethodologyTemplateRecordContent> entries = [Entry("First")];
        var content = Content(entries);
        Assert.True(scenario.Aggregate.Revise(0, entries, content, FirstAuthor).IsSuccess);
        Assert.True(scenario.Aggregate.Review(1, Reviewer, true, "Accept").IsSuccess);
        var count = scenario.PendingEvents.Count;

        // Act
        var revisionRetry = scenario.Aggregate.Revise(0, entries, content, FirstAuthor);
        var reviewRetry = scenario.Aggregate.Review(1, Reviewer, true, "Accept");
        var changedReview = scenario.Aggregate.Review(1, Reviewer, false, "Return instead");

        // Assert
        Assert.True(revisionRetry.IsSuccess);
        Assert.True(reviewRetry.IsSuccess);
        Assert.False(changedReview.IsSuccess);
        Assert.Equal(count, scenario.PendingEvents.Count);
        Assert.Equal("reviewed", scenario.Aggregate.Status);
        Assert.Equal(Reviewer, scenario.Aggregate.AcceptedReviewer);
    }

    [Fact]
    public void ShouldRefuseReplayGivenFactsBelongToAnotherDraft()
    {
        // Arrange
        var scenario = Opened();
        Assert.True(Revise(scenario.Aggregate, 0, "First", FirstAuthor).IsSuccess);

        // Act
        Action replay = () => new AggregateScenario<MethodologyTemplateDraft>(
            new MethodologyTemplateDraft(TemplateId, Uuid.CreateVersion4())).Given(scenario.PendingEvents.ToArray());

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    static AggregateScenario<MethodologyTemplateDraft> Opened()
    {
        var scenario = new AggregateScenario<MethodologyTemplateDraft>(new MethodologyTemplateDraft(TemplateId, DraftId));
        Assert.True(scenario.Aggregate.Open(Uuid.CreateVersion4(), 0, [], Content([])).IsSuccess);
        return scenario;
    }

    static Result Revise(MethodologyTemplateDraft draft, long expected, string summary, Uuid author)
    {
        IReadOnlyList<MethodologyTemplateRecordContent> entries = [Entry(summary)];
        return draft.Revise(expected, entries, Content(entries), author);
    }

    static MethodologyTemplateRecordContent Entry(string summary) => new("access-review", "control",
        "Review access", summary, []);

    static ImmutableContentRef Content(IReadOnlyList<MethodologyTemplateRecordContent> entries)
    {
        var info = ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(IReadOnlyList<MethodologyTemplateRecordContent>))!;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entries, info);
        return new ImmutableContentRef(Uuid.CreateVersion4(), ContentDigest.Sha256(bytes), bytes.LongLength,
            "application/json", MethodologyTemplateDraft.EntriesSchema);
    }
}
