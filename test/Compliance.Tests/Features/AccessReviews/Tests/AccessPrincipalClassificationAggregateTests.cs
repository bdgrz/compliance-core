using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

/// <summary>
///     Owner-level tests of one principal's classification history: identity, contiguous per-principal revision,
///     refusals that append nothing and replay of valid history.
/// </summary>
public sealed class AccessPrincipalClassificationAggregateTests
{
    static AccessPrincipalClassification Fresh(Uuid tenantId, Uuid populationId, string subject = "ada") =>
        new(tenantId, populationId, subject);

    static Result Classify(AccessPrincipalClassification owner, long expectedRevision,
        string commandKind = nameof(ClassifyAccessPrincipal)) => owner.Classify(
        NativeFacts.Root(owner, Uuid.CreateVersion4(), commandKind, expectedRevision), NativeFacts.Ref(),
        NativeFacts.Ref(), NativeFacts.Ref());

    static int Pending(AccessPrincipalClassification owner) =>
        new AggregateScenario<AccessPrincipalClassification>(owner).PendingEvents.Count;

    [Fact]
    public void ShouldKeepStreamOfPrincipalGivenTenantPopulationAndExactSubjectOnly()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();

        // Act
        var first = Fresh(tenantId, populationId);
        var again = Fresh(tenantId, populationId);
        var otherCase = Fresh(tenantId, populationId, "Ada");
        var otherPopulation = Fresh(tenantId, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(first.Id, again.Id);
        Assert.NotEqual(first.Id, otherCase.Id);
        Assert.NotEqual(first.Id, otherPopulation.Id);
        Assert.Equal(AccessPrincipalClassification.IdentityOf(tenantId, populationId, "ada"), first.Id);
        Assert.Equal(new EventStreamAddress(tenantId.ToString(), AccessPrincipalClassification.Area,
            first.Id.ToString()), first.Stream);
    }

    [Fact]
    public void ShouldRaiseContiguousDecisionsGivenMatchingExpectedRevisions()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var owner = Fresh(tenantId, populationId);

        // Act
        var first = Classify(owner, 0);
        var revisionAfterFirst = owner.Revision;
        var second = Classify(owner, 1);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        var pending = new AggregateScenario<AccessPrincipalClassification>(owner).PendingEvents;
        Assert.Equal(2, pending.Count);
        Assert.Equal(1, revisionAfterFirst);
        Assert.NotSame(pending[0], pending[1]);
        var earliest = Assert.IsType<AccessPrincipalClassified>(pending[0]);
        Assert.Equal("ada", earliest.ProviderSubjectId);
        var latest = Assert.IsType<AccessPrincipalClassified>(pending[1]);
        Assert.Equal(tenantId, latest.TenantId);
        Assert.Equal(populationId, latest.PopulationId);
        Assert.Equal("ada", latest.ProviderSubjectId);
        Assert.Equal(2, owner.Revision);
        Assert.Same(latest, owner.Latest);
    }

    [Fact]
    public void ShouldAppendNothingGivenStaleExpectedRevision()
    {
        // Arrange
        var owner = Fresh(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(Classify(owner, 0).IsSuccess);

        // Act
        var stale = Classify(owner, 0);
        var ahead = Classify(owner, 5);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, ahead.Error?.Kind);
        Assert.Equal(1, Pending(owner));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenOperationNotRootedInOwnPrincipalOrWrongCommand()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var owner = Fresh(tenantId, populationId);
        var elsewhere = Fresh(tenantId, populationId, "rae");

        // Act
        var otherStream = owner.Classify(
            NativeFacts.Root(elsewhere, Uuid.CreateVersion4(), nameof(ClassifyAccessPrincipal), 0),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        var withoutRevision = owner.Classify(
            NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(ClassifyAccessPrincipal), null),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        var wrongCommand = Classify(owner, 0, "ApproveAccessExpectation");

        // Assert
        Assert.Equal(RequestErrorKind.Validation, otherStream.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, withoutRevision.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, wrongCommand.Error?.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.Equal(0, owner.Revision);
        Assert.Null(owner.Latest);
    }

    [Fact]
    public void ShouldAppendNothingGivenBlankSubjectOrMissingPopulation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var blankSubject = Fresh(tenantId, Uuid.CreateVersion4(), " ");
        var noPopulation = Fresh(tenantId, Uuid.Empty);

        // Act
        var blank = Classify(blankSubject, 0);
        var missing = Classify(noPopulation, 0);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, blank.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, missing.Error?.Kind);
        Assert.Equal(0, Pending(blankSubject));
        Assert.Equal(0, Pending(noPopulation));
    }

    [Fact]
    public void ShouldRestoreRevisionAndLatestGivenValidDecisionHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var source = Fresh(tenantId, populationId);
        Assert.True(Classify(source, 0).IsSuccess);
        Assert.True(Classify(source, 1).IsSuccess);
        var facts = NativeFacts.Raised(source);

        // Act
        var owner = NativeFacts.Replay(Fresh(tenantId, populationId), facts);

        // Assert
        Assert.Equal(2, owner.Revision);
        Assert.Equal(populationId, owner.Latest!.PopulationId);
        Assert.Equal(0, Pending(owner));
    }

    [Fact]
    public void ShouldRefuseCompetingDecisionGivenHistoryAlreadyAdvancedPastExpectedRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var source = Fresh(tenantId, populationId);
        Assert.True(Classify(source, 0).IsSuccess);
        var owner = NativeFacts.Replay(Fresh(tenantId, populationId), NativeFacts.Raised(source));

        // Act
        var competing = Classify(owner, 0);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, competing.Error?.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.Equal(1, owner.Revision);
    }
}
