using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Projections;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class RuntimeCriteriaProjectionRulesTests
{
    [Fact]
    public void ShouldKeepPreviousIncarnationGivenPrePublicationReplacementRefusedAfterPublication()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var draft = Uuid.CreateVersion4();
        var old = Uuid.CreateVersion4();
        var current = Criterion(edition, draft, "CC6.1", old);
        var proposal = Proposal("replace", Uuid.CreateVersion4(), beforePublication: true);
        var pending = RuntimeCriteriaProjectionRules.Propose(current, proposal);

        // Act
        var result = RuntimeCriteriaProjectionRules.Settle(pending, proposal.OperationId,
            proposal.ExecutionGeneration, proposal.ProposalEventId, accepted: false, published: true);

        // Assert
        Assert.True(result.Included);
        Assert.Equal(old, result.IncarnationId);
        Assert.Equal(current.Summary, result.Summary);
        Assert.Null(result.Pending);
    }

    [Fact]
    public void ShouldApplyAcceptedPrePublicationIntentGivenOutcomeArrivesAfterPublication()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var draft = Uuid.CreateVersion4();
        var old = Uuid.CreateVersion4();
        var replacement = Uuid.CreateVersion4();
        var current = Criterion(edition, draft, "CC6.1", old);
        var proposal = Proposal("replace", replacement, beforePublication: true);
        var pending = RuntimeCriteriaProjectionRules.Propose(current, proposal);

        // Act
        var result = RuntimeCriteriaProjectionRules.Settle(pending, proposal.OperationId,
            proposal.ExecutionGeneration, proposal.ProposalEventId, accepted: true, published: true);

        // Assert
        Assert.True(result.Included);
        Assert.Equal(replacement, result.IncarnationId);
        Assert.Equal("Replacement summary", result.Summary);
        Assert.Null(result.Pending);
    }

    [Fact]
    public void ShouldRejectAcceptedPostPublicationStructuralChangeGivenPublishedCut()
    {
        // Arrange
        var current = Criterion(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "CC6.1", Uuid.CreateVersion4());
        var proposal = Proposal("remove", Uuid.CreateVersion4(), beforePublication: false);
        var pending = RuntimeCriteriaProjectionRules.Propose(current, proposal);

        // Act / Assert
        // Assert
        Assert.Throws<InvalidOperationException>(() => RuntimeCriteriaProjectionRules.Settle(pending,
            proposal.OperationId, proposal.ExecutionGeneration, proposal.ProposalEventId, accepted: true, published: true));
    }

    [Fact]
    public void ShouldKeepOldFocusIneffectiveGivenParentIdentifierReaddedWithNewIncarnation()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var draft = Uuid.CreateVersion4();
        var originalParent = Uuid.CreateVersion4();
        var laterParent = Criterion(edition, draft, "CC6.1", Uuid.CreateVersion4());
        var focus = new RuntimeCriteriaEntryIndexRow("live", edition, "bdgrz:focus-1", draft, true,
            Uuid.CreateVersion4(), "point_of_focus", "security", "CC6.1", originalParent,
            "Focus summary", null);

        // Act
        var effective = RuntimeCriteriaProjectionRules.IsEffective(focus, laterParent);

        // Assert
        Assert.False(effective);
    }

    [Fact]
    public void ShouldRefuseMismatchedOutcomeGivenDifferentProposalEvent()
    {
        // Arrange
        var current = Criterion(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "CC6.1", Uuid.CreateVersion4());
        var proposal = Proposal("replace", Uuid.CreateVersion4(), beforePublication: true);
        var pending = RuntimeCriteriaProjectionRules.Propose(current, proposal);

        // Act / Assert
        // Assert
        Assert.Throws<InvalidOperationException>(() => RuntimeCriteriaProjectionRules.Settle(pending,
            proposal.OperationId, proposal.ExecutionGeneration, Uuid.CreateVersion4(), accepted: true, published: true));
    }

    static RuntimeCriteriaEntryIndexRow Criterion(Uuid edition, Uuid draft, string identifier, Uuid incarnation) =>
        new("live", edition, identifier, draft, true, incarnation, "criterion", "security", null, null,
            "Original summary", null);

    static RuntimeCriteriaPendingIndex Proposal(string action, Uuid incarnation, bool beforePublication) =>
        new(Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), beforePublication, action, incarnation,
            "criterion", "security", null, null, "Replacement summary");
}
