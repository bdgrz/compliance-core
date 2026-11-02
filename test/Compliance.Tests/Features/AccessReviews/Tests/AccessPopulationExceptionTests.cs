using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

/// <summary>
///     Owner-level tests of the missing-population exception: creation once by its own root operation, refusals that
///     append nothing, independence of unrelated exceptions, and replay of valid history.
/// </summary>
public sealed class AccessPopulationExceptionTests
{
    static AccessPopulationException Fresh(Uuid tenantId, Uuid exceptionId) =>
        new(tenantId, exceptionId, exceptionId);

    static Result Record(AccessPopulationException owner, Uuid systemInstanceId, long? expectedRevision = 0) =>
        owner.Record(NativeFacts.Root(owner, owner.Id, "ExemptMissingAccessPopulation", expectedRevision),
            systemInstanceId, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

    static int Pending(AccessPopulationException owner) =>
        new AggregateScenario<AccessPopulationException>(owner).PendingEvents.Count;

    [Fact]
    public void ShouldKeepStreamOfExceptionGivenTenantAndExceptionIdsOnly()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var exceptionId = Uuid.CreateVersion4();

        // Act
        var first = new AccessPopulationException(tenantId, exceptionId, Uuid.CreateVersion4());
        var second = new AccessPopulationException(tenantId, exceptionId);

        // Assert
        var expected = new EventStreamAddress(tenantId.ToString(), AccessPopulationException.Area,
            exceptionId.ToString());
        Assert.Equal(expected, first.Stream);
        Assert.Equal(expected, second.Stream);
        Assert.Equal(0, first.Revision);
    }

    [Fact]
    public void ShouldRecordRevisionOneExceptionGivenValidRootOperation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var exceptionId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var owner = Fresh(tenantId, exceptionId);

        // Act
        var result = Record(owner, instanceId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var recorded = Assert.IsType<AccessPopulationExceptionRecorded>(
            Assert.Single(new AggregateScenario<AccessPopulationException>(owner).PendingEvents));
        Assert.Equal(tenantId, recorded.TenantId);
        Assert.Equal(instanceId, recorded.SystemInstanceId);
        Assert.Equal(1, recorded.Revision);
        Assert.Equal(exceptionId, recorded.Operation.LogicalId);
        Assert.Equal(instanceId, owner.SystemInstanceId);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenRecordNotRootedInExceptionOrMissingBusinessFacts()
    {
        // Arrange
        var owner = Fresh(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var elsewhere = Fresh(owner.Id, Uuid.CreateVersion4());

        // Act
        var otherLogical = owner.Record(NativeFacts.Root(owner, Uuid.CreateVersion4(), "ExemptMissingAccessPopulation", 0),
            Uuid.CreateVersion4(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        var otherStream = owner.Record(NativeFacts.Root(elsewhere, owner.Id, "ExemptMissingAccessPopulation", 0),
            Uuid.CreateVersion4(), NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        var withoutRevision = Record(owner, Uuid.CreateVersion4(), null);
        var noInstance = Record(owner, Uuid.Empty);
        var nonZero = Record(owner, Uuid.CreateVersion4(), 3);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, otherLogical.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, otherStream.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, withoutRevision.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, noInstance.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, nonZero.Error?.Kind);
        Assert.Contains("expects revision 0", nonZero.Error?.Message, StringComparison.Ordinal);
        Assert.Equal(0, Pending(owner));
        Assert.Null(owner.Recorded);
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseSecondRecordGivenExceptionAlreadyRecorded()
    {
        // Arrange
        var owner = Fresh(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var instanceId = Uuid.CreateVersion4();
        Assert.True(Record(owner, instanceId).IsSuccess);

        // Act
        var second = Record(owner, Uuid.CreateVersion4());
        var prepared = owner.PrepareRecord(0);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, second.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, prepared.Error?.Kind);
        Assert.Equal(1, Pending(owner));
        Assert.Equal(instanceId, owner.SystemInstanceId);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldNotContendGivenUnrelatedExceptionsInTheSameTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var first = Fresh(tenantId, Uuid.CreateVersion4());
        var second = Fresh(tenantId, Uuid.CreateVersion4());
        var instanceId = Uuid.CreateVersion4();

        // Act
        var firstResult = Record(first, instanceId);
        var secondResult = Record(second, instanceId);

        // Assert
        Assert.True(firstResult.IsSuccess, firstResult.Error?.Message);
        Assert.True(secondResult.IsSuccess, secondResult.Error?.Message);
        Assert.NotEqual(first.Stream, second.Stream);
        Assert.Equal(1, Pending(first));
        Assert.Equal(1, Pending(second));
    }

    [Fact]
    public void ShouldRestoreExceptionStateGivenValidRecordedHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var exceptionId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var seed = Fresh(tenantId, exceptionId);
        Assert.True(Record(seed, instanceId).IsSuccess);
        var facts = NativeFacts.Raised(seed);

        // Act
        var owner = NativeFacts.Replay(Fresh(tenantId, exceptionId), facts);

        // Assert
        Assert.Equal(1, owner.Revision);
        Assert.Equal(instanceId, owner.SystemInstanceId);
        Assert.NotNull(owner.Recorded);
        Assert.Equal(0, Pending(owner));
    }

    [Fact]
    public void ShouldRefuseCompetingRecordGivenHistoryAlreadyRecorded()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var exceptionId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var seed = Fresh(tenantId, exceptionId);
        Assert.True(Record(seed, instanceId).IsSuccess);
        var owner = NativeFacts.Replay(Fresh(tenantId, exceptionId), NativeFacts.Raised(seed));

        // Act
        var competing = Record(owner, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, competing.Error?.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.Equal(instanceId, owner.SystemInstanceId);
        Assert.Equal(1, owner.Revision);
    }
}
