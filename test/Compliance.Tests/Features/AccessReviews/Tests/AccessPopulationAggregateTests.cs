using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

/// <summary>
///     Owner-level tests of the access population: opening, draft facts replacement and acceptance guards, refusals
///     that append nothing, and replay of valid history.
/// </summary>
public sealed class AccessPopulationAggregateTests
{
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Attester");

    static AccessPopulation Fresh(Uuid tenantId, Uuid populationId) =>
        new(tenantId, populationId, populationId);

    static Result Open(AccessPopulation owner, Uuid? applicationId = null, string source = "Export") =>
        owner.Open(NativeFacts.Root(owner, owner.Id, "OpenAccessPopulation", null),
            applicationId ?? Uuid.CreateVersion4(), Uuid.CreateVersion4(), 3, NativeFacts.Now.AddDays(-1), source,
            Actor, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());

    static Result Record(AccessPopulation owner, long expectedRevision) =>
        owner.Record(NativeFacts.Root(owner, Uuid.CreateVersion4(), "RecordAccessPopulationFacts", expectedRevision),
            NativeFacts.Ref(), Actor, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());

    static Result Accept(AccessPopulation owner, long expectedRevision, string attestation = "Attested.") =>
        owner.Accept(NativeFacts.Root(owner, Uuid.CreateVersion4(), "AcceptAccessPopulation", expectedRevision),
            Uuid.CreateVersion4(), new string('c', 64), Uuid.CreateVersion4(), attestation, Actor, NativeFacts.Now,
            NativeFacts.Ref(), NativeFacts.Ref());

    static AccessPopulation OpenedDraft(Uuid tenantId, Uuid populationId)
    {
        var owner = Fresh(tenantId, populationId);
        Assert.True(Open(owner).IsSuccess);
        return owner;
    }

    static int Pending(AccessPopulation owner) => new AggregateScenario<AccessPopulation>(owner).PendingEvents.Count;

    [Fact]
    public void ShouldRaiseRevisionOneOpeningGivenValidRootOperation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var owner = Fresh(tenantId, populationId);

        // Act
        var result = Open(owner);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var opened = Assert.IsType<AccessPopulationOpened>(
            Assert.Single(new AggregateScenario<AccessPopulation>(owner).PendingEvents));
        Assert.Equal(tenantId, opened.TenantId);
        Assert.Equal(populationId, opened.PopulationId);
        Assert.Equal(populationId, opened.Operation.LogicalId);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(AccessPopulation.Draft, owner.Status);
        Assert.Null(owner.FactsContent);
    }

    [Fact]
    public void ShouldAppendNothingGivenOpeningNotRootedInPopulationOrMissingBusinessFacts()
    {
        // Arrange
        var owner = Fresh(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var elsewhere = Fresh(owner.Id, Uuid.CreateVersion4());

        // Act
        var otherLogical = owner.Open(NativeFacts.Root(owner, Uuid.CreateVersion4(), "OpenAccessPopulation", null),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, NativeFacts.Now, "Export", Actor, NativeFacts.Now,
            NativeFacts.Ref(), NativeFacts.Ref());
        var otherStream = owner.Open(NativeFacts.Root(elsewhere, owner.Id, "OpenAccessPopulation", null),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, NativeFacts.Now, "Export", Actor, NativeFacts.Now,
            NativeFacts.Ref(), NativeFacts.Ref());
        var noApplication = Open(owner, Uuid.Empty);
        var noInstance = owner.Open(NativeFacts.Root(owner, owner.Id, "OpenAccessPopulation", null),
            Uuid.CreateVersion4(), Uuid.Empty, 1, NativeFacts.Now, "Export", Actor, NativeFacts.Now,
            NativeFacts.Ref(), NativeFacts.Ref());
        var blankSource = Open(owner, source: " ");

        // Assert
        Assert.Equal(RequestErrorKind.Validation, otherLogical.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, otherStream.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, noApplication.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, noInstance.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, blankSource.Error?.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.False(owner.IsOpened);
    }

    [Fact]
    public void ShouldRefuseSecondOpeningGivenPopulationAlreadyOpened()
    {
        // Arrange
        var owner = OpenedDraft(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var second = Open(owner, source: "Another export");

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, second.Error?.Kind);
        Assert.Equal(1, Pending(owner));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldReplaceFactsAtNextRevisionGivenDraftAndMatchingExpectedRevision()
    {
        // Arrange
        var owner = OpenedDraft(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var first = Record(owner, 1);
        var second = Record(owner, 2);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        var pending = new AggregateScenario<AccessPopulation>(owner).PendingEvents;
        Assert.Equal(3, pending.Count);
        Assert.Equal(2, Assert.IsType<AccessPopulationFactsRecorded>(pending[1]).Revision);
        Assert.Equal(3, Assert.IsType<AccessPopulationFactsRecorded>(pending[2]).Revision);
        Assert.Equal(3, owner.Revision);
        Assert.NotNull(owner.FactsContent);
        Assert.False(owner.IsAccepted);
    }

    [Fact]
    public void ShouldAppendNothingGivenFactsForUnopenedStaleOrUnrootedRequest()
    {
        // Arrange
        var unopened = Fresh(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var owner = OpenedDraft(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var notFound = Record(unopened, 1);
        var stale = Record(owner, 7);
        var withoutRevision = owner.Record(NativeFacts.Root(owner, Uuid.CreateVersion4(), "RecordAccessPopulationFacts",
            null), NativeFacts.Ref(), Actor, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notFound.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Contains("revision", stale.Error?.Message, StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.Validation, withoutRevision.Error?.Kind);
        Assert.Equal(0, Pending(unopened));
        Assert.Equal(1, Pending(owner));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseAcceptanceGivenDraftWithoutRecordedFacts()
    {
        // Arrange
        var owner = OpenedDraft(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var result = Accept(owner, 1);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(1, Pending(owner));
        Assert.False(owner.IsAccepted);
    }

    [Fact]
    public void ShouldAppendNothingGivenAcceptanceMissingNotFoundStaleOrIncompleteSnapshot()
    {
        // Arrange
        var unopened = Fresh(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var owner = OpenedDraft(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(Record(owner, 1).IsSuccess);

        // Act
        var notFound = Accept(unopened, 1);
        var stale = Accept(owner, 1);
        var blankAttestation = Accept(owner, 2, " ");
        var noSnapshot = owner.Accept(NativeFacts.Root(owner, Uuid.CreateVersion4(), "AcceptAccessPopulation", 2),
            Uuid.Empty, new string('c', 64), Uuid.CreateVersion4(), "Attested.", Actor, NativeFacts.Now,
            NativeFacts.Ref(), NativeFacts.Ref());
        var blankHash = owner.Accept(NativeFacts.Root(owner, Uuid.CreateVersion4(), "AcceptAccessPopulation", 2),
            Uuid.CreateVersion4(), " ", Uuid.CreateVersion4(), "Attested.", Actor, NativeFacts.Now,
            NativeFacts.Ref(), NativeFacts.Ref());
        var noCalculation = owner.Accept(NativeFacts.Root(owner, Uuid.CreateVersion4(), "AcceptAccessPopulation", 2),
            Uuid.CreateVersion4(), new string('c', 64), Uuid.Empty, "Attested.", Actor, NativeFacts.Now,
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notFound.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, blankAttestation.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, noSnapshot.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, blankHash.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, noCalculation.Error?.Kind);
        Assert.Equal(2, Pending(owner));
        Assert.False(owner.IsAccepted);
    }

    [Fact]
    public void ShouldFreezePopulationGivenAcceptanceAndRefuseFurtherChanges()
    {
        // Arrange
        var owner = OpenedDraft(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(Record(owner, 1).IsSuccess);

        // Act
        var accepted = Accept(owner, 2);
        var recordAfter = Record(owner, 3);
        var acceptAgain = Accept(owner, 3);

        // Assert
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, recordAfter.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, acceptAgain.Error?.Kind);
        Assert.Equal(3, Pending(owner));
        Assert.Equal(3, owner.Revision);
        Assert.Equal(AccessPopulation.Accepted, owner.Status);
        Assert.Equal(3, owner.AcceptedFact!.Revision);
    }

    [Fact]
    public void ShouldRestorePopulationStateGivenValidOpenedRecordedAndAcceptedHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var source = Fresh(tenantId, populationId);
        Assert.True(Open(source).IsSuccess);
        Assert.True(Record(source, 1).IsSuccess);
        Assert.True(Accept(source, 2).IsSuccess);
        var facts = NativeFacts.Raised(source);

        // Act
        var owner = NativeFacts.Replay(Fresh(tenantId, populationId), facts);

        // Assert
        Assert.Equal(3, owner.Revision);
        Assert.True(owner.IsOpened);
        Assert.True(owner.IsAccepted);
        Assert.NotNull(owner.FactsContent);
        Assert.Equal(populationId, owner.Opened!.PopulationId);
        Assert.Equal(AccessPopulation.Accepted, owner.Status);
        Assert.Equal(0, Pending(owner));
    }

    [Fact]
    public void ShouldRefuseCompetingReplacementGivenHistoryAlreadyAdvancedPastExpectedRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var source = Fresh(tenantId, populationId);
        Assert.True(Open(source).IsSuccess);
        Assert.True(Record(source, 1).IsSuccess);
        var owner = NativeFacts.Replay(Fresh(tenantId, populationId), NativeFacts.Raised(source));

        // Act
        var competing = Record(owner, 1);
        var next = Record(owner, 2);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, competing.Error?.Kind);
        Assert.Contains("revision", competing.Error?.Message, StringComparison.Ordinal);
        Assert.True(next.IsSuccess, next.Error?.Message);
        Assert.Equal(1, Pending(owner));
        Assert.Equal(3, owner.Revision);
    }

    [Fact]
    public void ShouldKeepStreamOfPopulationGivenTenantAndPopulationIdsOnly()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();

        // Act
        var first = new AccessPopulation(tenantId, populationId, Uuid.CreateVersion4());
        var second = new AccessPopulation(tenantId, populationId, Uuid.CreateVersion4());

        // Assert
        var expected = new EventStreamAddress(tenantId.ToString(), AccessPopulation.Area, populationId.ToString());
        Assert.Equal(expected, first.Stream);
        Assert.Equal(expected, second.Stream);
    }
}
