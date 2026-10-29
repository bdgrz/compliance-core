using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;

/// <summary>The feedback thread owner's own decisions: materialization, write checks and replay of valid history.</summary>
public sealed class AdvisorFeedbackThreadAggregateTests
{
    static readonly ActorReference Advisor = ActorReference.ForMember(Uuid.CreateVersion4(), "Advisor");

    [Fact]
    public void ShouldDeriveStableIdentityGivenSameTenantProgramHandoffAndFeedback()
    {
        // Arrange
        var (tenantId, programId, handoffId, feedbackId) = Ids();

        // Act
        var first = new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId);
        var again = new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId);
        var other = new AdvisorFeedbackThread(tenantId, programId, handoffId, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(first.Stream, again.Stream);
        Assert.NotEqual(first.Id, other.Id);
        Assert.NotEqual(first.Stream, other.Stream);
    }

    [Fact]
    public void ShouldAppendRecordedFactAtRevisionOneGivenAdmittedContribution()
    {
        // Arrange
        var (tenantId, programId, handoffId, feedbackId) = Ids();
        var owner = new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId);
        var authored = Authored(tenantId, programId, handoffId, feedbackId, "validated");

        // Act
        var result = Materialize(owner, authored);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var fact = Assert.IsType<AdvisorFeedbackRecorded>(Assert.Single(Facts(owner)));
        Assert.Equal(feedbackId, fact.FeedbackId);
        Assert.Equal(authored.RecordId, fact.RecordId);
        Assert.Equal(authored.RecordVersionId, fact.RecordVersionId);
        Assert.Equal("validated", fact.Kind);
        Assert.Equal(Advisor, fact.Author);
        Assert.True(owner.IsMaterialized);
        Assert.Equal(1, owner.Revision);
    }

    [Theory]
    [InlineData("approval")]
    [InlineData("")]
    [InlineData("Comment")]
    public void ShouldRefuseMaterializationAndAppendNothingGivenKindOutsideTheCatalog(string kind)
    {
        // Arrange
        var (tenantId, programId, handoffId, feedbackId) = Ids();
        var owner = new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId);

        // Act
        var result = Materialize(owner, Authored(tenantId, programId, handoffId, feedbackId, kind));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(Facts(owner));
        Assert.False(owner.IsMaterialized);
    }

    [Fact]
    public void ShouldRefuseMaterializationAndAppendNothingGivenContributionOfAnotherFeedback()
    {
        // Arrange
        var (tenantId, programId, handoffId, feedbackId) = Ids();
        var owner = new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId);
        var otherFeedback = Authored(tenantId, programId, handoffId, Uuid.CreateVersion4(), "comment");
        var otherHandoff = Authored(tenantId, programId, Uuid.CreateVersion4(), feedbackId, "comment");
        var otherTenant = Authored(Uuid.CreateVersion4(), programId, handoffId, feedbackId, "comment");

        // Act
        var results = new[]
        {
            Materialize(owner, otherFeedback), Materialize(owner, otherHandoff), Materialize(owner, otherTenant),
        };

        // Assert
        Assert.All(results, result => Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind));
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldRefuseMaterializationAndAppendNothingGivenEmptyRecordIdentity()
    {
        // Arrange
        var (tenantId, programId, handoffId, feedbackId) = Ids();
        var owner = new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId);
        var noRecord = Authored(tenantId, programId, handoffId, feedbackId, "comment") with { RecordId = Uuid.Empty };
        var noVersion = Authored(tenantId, programId, handoffId, feedbackId, "comment") with
        {
            RecordVersionId = Uuid.Empty,
        };
        var unsupportedSource = Authored(tenantId, programId, handoffId, feedbackId, "comment") with
        {
            SelectedSource = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.Empty),
        };

        // Act
        var results = new[]
        {
            Materialize(owner, noRecord), Materialize(owner, noVersion), Materialize(owner, unsupportedSource),
        };

        // Assert
        Assert.All(results, result => Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind));
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldRefuseMaterializationAndAppendNothingGivenOperationOnAnotherStream()
    {
        // Arrange
        var (tenantId, programId, handoffId, feedbackId) = Ids();
        var owner = new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId);
        var elsewhere = new AdvisorFeedbackThread(tenantId, programId, handoffId, Uuid.CreateVersion4());
        var foreign = NativeFacts.Root(elsewhere, feedbackId, "MaterializeAdvisorFeedback", null);

        // Act
        var result = owner.Materialize(foreign, Authored(tenantId, programId, handoffId, feedbackId, "comment"),
            Advisor, NativeFacts.Now, NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldRefuseSecondMaterializationGivenThreadAlreadyRecorded()
    {
        // Arrange
        var (tenantId, programId, handoffId, feedbackId) = Ids();
        var owner = new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId);
        var authored = Authored(tenantId, programId, handoffId, feedbackId, "comment");
        Assert.True(Materialize(owner, authored).IsSuccess);

        // Act
        var result = Materialize(owner, authored);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(Facts(owner));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldReportNotFoundGivenThreadNeverMaterialized()
    {
        // Arrange
        var (tenantId, programId, handoffId, feedbackId) = Ids();
        var owner = new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId);

        // Act
        var results = new[] { owner.PrepareWrite(0), owner.PrepareResolve(0) };

        // Assert
        Assert.All(results, result => Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind));
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseWriteGivenStaleExpectedRevisionAfterLaterFacts()
    {
        // Arrange
        var owner = Replay("respond", "correct");

        // Act
        var stale = owner.PrepareWrite(1);
        var ahead = owner.PrepareWrite(4);
        var current = owner.PrepareWrite(3);
        var staleResolve = owner.PrepareResolve(2);

        // Assert
        Assert.Equal(3, owner.Revision);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, ahead.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, staleResolve.Error?.Kind);
        Assert.True(current.IsSuccess, current.Error?.Message);
    }

    [Fact]
    public void ShouldRefuseResolutionGivenThreadAlreadyResolved()
    {
        // Arrange
        var owner = Replay("respond", "resolve");

        // Act
        var again = owner.PrepareResolve(3);

        // Assert
        Assert.NotNull(owner.Resolved);
        Assert.Equal(3, owner.Revision);
        Assert.Equal(RequestErrorKind.Conflict, again.Error?.Kind);
    }

    [Fact]
    public void ShouldRefuseCorrectionAndAppendNothingGivenEmptyCorrectionIdentity()
    {
        // Arrange
        var owner = Replay();
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), "CorrectAdvisorFeedback", 1);

        // Act
        var result = owner.Correct(operation, Uuid.Empty, NativeFacts.Ref(), Advisor, NativeFacts.Now,
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(owner));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseCorrectionAndAppendNothingGivenOperationOfAnotherLogicalId()
    {
        // Arrange
        var owner = Replay();
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), "CorrectAdvisorFeedback", 1);

        // Act
        var result = owner.Correct(operation, Uuid.CreateVersion4(), NativeFacts.Ref(), Advisor, NativeFacts.Now,
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldRefuseWriteAndAppendNothingGivenOperationWithoutExpectedRevision()
    {
        // Arrange
        var owner = Replay();
        var correctionId = Uuid.CreateVersion4();
        var operation = NativeFacts.Root(owner, correctionId, "CorrectAdvisorFeedback", null);

        // Act
        var result = owner.Correct(operation, correctionId, NativeFacts.Ref(), Advisor, NativeFacts.Now,
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldKeepOriginalContributionAndAdvanceRevisionGivenReplayOfCorrectionResponseAndResolution()
    {
        // Arrange
        var (tenantId, programId, handoffId, feedbackId) = Ids();

        // Act
        var owner = Replay(tenantId, programId, handoffId, feedbackId, "correct", "respond", "resolve");

        // Assert
        Assert.True(owner.IsMaterialized);
        Assert.Equal(4, owner.Revision);
        Assert.Equal("comment", owner.Recorded!.Kind);
        Assert.Equal(feedbackId, owner.Recorded.FeedbackId);
        Assert.Equal(handoffId, owner.HandoffId);
        Assert.NotNull(owner.Resolved);
        Assert.Equal(Advisor, owner.Resolved!.ResolvedBy);
    }

    static (Uuid TenantId, Uuid ProgramId, Uuid HandoffId, Uuid FeedbackId) Ids() => (Uuid.CreateVersion4(),
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());

    static RecordAdvisorFeedback Authored(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId,
        string kind) => new(tenantId, programId, handoffId, feedbackId, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
        kind, "Please clarify the control owner.", 1);

    static Result Materialize(AdvisorFeedbackThread owner, RecordAdvisorFeedback authored) => owner.Materialize(
        NativeFacts.Root(owner, authored.FeedbackId, "MaterializeAdvisorFeedback", null), authored, Advisor,
        NativeFacts.Now, NativeFacts.Ref());

    static DomainEvent[] Facts(AdvisorFeedbackThread owner) =>
        new AggregateScenario<AdvisorFeedbackThread>(owner).PendingEvents.ToArray();

    static AdvisorFeedbackThread Replay(params string[] steps)
    {
        var (tenantId, programId, handoffId, feedbackId) = Ids();
        return Replay(tenantId, programId, handoffId, feedbackId, steps);
    }

    /// <summary>A thread replayed from a valid recorded fact followed by the named steps.</summary>
    static AdvisorFeedbackThread Replay(Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId,
        params string[] steps)
    {
        var shell = new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId);
        var facts = new List<DomainEvent>
        {
            DomainEventSeed.Attach(new AdvisorFeedbackRecorded(tenantId, programId, handoffId, feedbackId,
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), "comment", Advisor, NativeFacts.Now,
                NativeFacts.Root(shell, feedbackId, "MaterializeAdvisorFeedback", null), NativeFacts.Ref(), null, null),
                shell.Id, 1),
        };
        var revision = 1L;
        foreach (var step in steps)
        {
            revision++;
            var logicalId = Uuid.CreateVersion4();
            var operation = NativeFacts.Root(shell, logicalId, step, revision - 1);
            if (step == "correct")
                facts.Add(DomainEventSeed.Attach(new AdvisorFeedbackCorrected(tenantId, programId, handoffId,
                    feedbackId, logicalId, NativeFacts.Ref(), Advisor, NativeFacts.Now, operation, NativeFacts.Ref(),
                    NativeFacts.Ref()), shell.Id, (ulong)facts.Count + 1));
            else if (step == "respond")
                facts.Add(DomainEventSeed.Attach(new AdvisorFeedbackResponded(tenantId, programId, handoffId,
                    feedbackId, NativeFacts.Ref(), Advisor, NativeFacts.Now, operation, NativeFacts.Ref(),
                    NativeFacts.Ref()), shell.Id, (ulong)facts.Count + 1));
            else
                facts.Add(DomainEventSeed.Attach(new AdvisorFeedbackResolved(tenantId, programId, handoffId,
                    feedbackId, Advisor, NativeFacts.Now, operation, NativeFacts.Ref(), NativeFacts.Ref()),
                    shell.Id, (ulong)facts.Count + 1));
        }

        return NativeFacts.Replay(new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId), facts);
    }
}
