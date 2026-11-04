using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Versioning.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Boundaries.Tests;

public sealed class CanonicalBoundaryLifecycleTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRefuseWithoutFactGivenBoundaryIdentityDiffersFromProgram()
    {
        // Arrange
        var rules = new BoundaryRules(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var result = rules.DraftBoundary(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0,
            Content(), Uuid.CreateVersion4(), "Author", Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(rules.IsCreated);
        Assert.Empty(rules.PendingChanges);
    }

    [Fact]
    public void ShouldApproveInOneReviewGivenIndependentReviewerAndExactDraft()
    {
        // Arrange
        var program = Uuid.CreateVersion4();
        var rules = new BoundaryRules(Uuid.CreateVersion4(), program);
        var version = Uuid.CreateVersion4();
        Assert.True(rules.DraftBoundary(program, version, 0, Content(), Uuid.CreateVersion4(), "Author", Now).IsSuccess);
        rules.ClearPendingChanges();

        // Act
        var failure = rules.ReviewDraft(version, 1, Uuid.CreateVersion4(), "approve", "Approved exact scope",
            new DateOnly(2026, 11, 5), Uuid.CreateVersion4(), "Lead", Now.AddMinutes(1));

        // Assert
        Assert.Null(failure);
        Assert.Equal(version, rules.LatestApprovedVersionId);
        Assert.Equal(Uuid.Empty, rules.DraftVersionId);
        Assert.IsType<BoundaryApprovedContent>(Assert.Single(rules.PendingChanges));
    }

    [Fact]
    public void ShouldRefuseEarlierAuthorWithoutFactGivenAnotherMemberRevisedDraft()
    {
        // Arrange
        var program = Uuid.CreateVersion4();
        var rules = new BoundaryRules(Uuid.CreateVersion4(), program);
        var version = Uuid.CreateVersion4();
        var firstAuthor = Uuid.CreateVersion4();
        Assert.True(rules.DraftBoundary(program, version, 0, Content(), firstAuthor, "First", Now).IsSuccess);
        Assert.Null(rules.Revise(version, 1, Content() with { Statement = "Revised" }, Uuid.CreateVersion4(), "Second", Now.AddMinutes(1)));
        rules.ClearPendingChanges();

        // Act
        var failure = rules.ReviewDraft(version, 2, Uuid.CreateVersion4(), "approve", "Looks good",
            new DateOnly(2026, 11, 5), firstAuthor, "First", Now.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(version, rules.DraftVersionId);
        Assert.Empty(rules.PendingChanges);
    }

    [Fact]
    public void ShouldPermitNewDraftGivenRequestedChangesThenDiscardedNeverApprovedDraft()
    {
        // Arrange
        var program = Uuid.CreateVersion4();
        var rules = new BoundaryRules(Uuid.CreateVersion4(), program);
        var version = Uuid.CreateVersion4();
        Assert.True(rules.DraftBoundary(program, version, 0, Content(), Uuid.CreateVersion4(), "Author", Now).IsSuccess);
        Assert.Null(rules.ReviewDraft(version, 1, Uuid.CreateVersion4(), "request_changes", "Clarify scope", null,
            Uuid.CreateVersion4(), "Lead", Now.AddMinutes(1)));
        Assert.Null(rules.DiscardDraft(version, 1, "Restart scope", Uuid.CreateVersion4(), "Lead", Now.AddMinutes(2)));
        rules.ClearPendingChanges();
        var next = Uuid.CreateVersion4();

        // Act
        var result = rules.DraftBoundary(program, next, rules.Revision, Content(), Uuid.CreateVersion4(), "New author", Now.AddMinutes(3));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(next, rules.DraftVersionId);
        Assert.IsType<BoundarySuccessorProposedContent>(Assert.Single(rules.PendingChanges));
    }

    [Fact]
    public void ShouldRefuseApprovalWithoutFactGivenNonIncreasingEffectiveDate()
    {
        // Arrange
        var program = Uuid.CreateVersion4();
        var rules = new BoundaryRules(Uuid.CreateVersion4(), program);
        var version = Uuid.CreateVersion4();
        var effective = new DateOnly(2026, 11, 5);
        Assert.True(rules.DraftBoundary(program, version, 0, Content(), Uuid.CreateVersion4(), "Author", Now).IsSuccess);
        Assert.Null(rules.ReviewDraft(version, 1, Uuid.CreateVersion4(), "approve", "Approve", effective,
            Uuid.CreateVersion4(), "Lead", Now.AddMinutes(1)));
        var next = Uuid.CreateVersion4();
        Assert.True(rules.DraftBoundary(program, next, rules.Revision, Content(), Uuid.CreateVersion4(), "Author", Now.AddMinutes(2)).IsSuccess);
        rules.ClearPendingChanges();

        // Act
        var failure = rules.ReviewDraft(next, 1, Uuid.CreateVersion4(), "approve", "Approve", effective,
            Uuid.CreateVersion4(), "Lead", Now.AddMinutes(3));

        // Assert
        Assert.NotNull(failure);
        Assert.Equal(version, rules.LatestApprovedVersionId);
        Assert.Equal(next, rules.DraftVersionId);
        Assert.Empty(rules.PendingChanges);
    }

    static BoundaryContent Content() => new("System scope", "readiness", ["security"], []);
}
