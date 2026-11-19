using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class FindingClosureProposalWorkTests
{
    [Fact]
    public void ShouldCreateReviewForZeroActionProposalGivenCurrentClosingSource()
    {
        // Arrange
        var author = Uuid.CreateVersion4();
        var state = State() with { Proposal = new FindingClosureProposalWorkState(Uuid.CreateVersion4(), 1, [author], Now) };

        // Act
        var candidate = FindingClosureWork.Candidate(state);

        // Assert
        Assert.NotNull(candidate);
        Assert.Equal("review", candidate.NextAction);
        Assert.Contains(author, candidate.Excluded);
        Assert.DoesNotContain(state.OwnerMemberId, candidate.Excluded);
    }

    [Fact]
    public void ShouldKeepAssignmentRoundGivenUnrelatedFindingRevisionAndRestartGivenProposalRevision()
    {
        // Arrange
        var state = State() with { Proposal = new FindingClosureProposalWorkState(Uuid.CreateVersion4(), 1, [Uuid.CreateVersion4()], Now) };

        // Act
        var original = FindingClosureWork.Candidate(state)!;
        var revisedFinding = FindingClosureWork.Candidate(state with { Revision = state.Revision + 1 })!;
        var revisedProposal = FindingClosureWork.Candidate(state with { Proposal = state.Proposal! with { Revision = 2 } })!;
        var withdrawn = FindingClosureWork.Candidate(state with { Proposal = null });

        // Assert
        Assert.Equal(original.WorkItemId, revisedFinding.WorkItemId);
        Assert.NotEqual(original.WorkItemId, revisedProposal.WorkItemId);
        Assert.Null(withdrawn);
    }

    [Fact]
    public void ShouldExcludeEveryProposalAuthorGivenRevisedClosureProposal()
    {
        // Arrange
        var authors = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var state = State() with { Proposal = new FindingClosureProposalWorkState(Uuid.CreateVersion4(), 2, authors, Now) };

        // Act
        var candidate = FindingClosureWork.Candidate(state)!;

        // Assert
        Assert.True(candidate.Excluded.SetEquals(authors));
        Assert.Null(FindingClosureWork.Candidate(state with { Closed = true }));
    }

    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static FindingClosureWorkState State() => new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
        1, "Access gap", "high", Uuid.CreateVersion4(), new DateOnly(2026, 11, 4), [], false, Now);
}
