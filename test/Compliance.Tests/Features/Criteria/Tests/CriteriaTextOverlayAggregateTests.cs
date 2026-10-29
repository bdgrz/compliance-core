using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

/// <summary>Owner-level proof of the per-criterion overlay revision line: identity, revision guard, replay.</summary>
public sealed class CriteriaTextOverlayAggregateTests
{
    const string CommandKind = "SetCriteriaTextOverlay";

    [Fact]
    public void ShouldDeriveStableIdentityGivenSameAndDifferentCriteria()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var first = new CriteriaTextOverlay(tenant, edition, "CC6.1");
        var sameWithOperation = new CriteriaTextOverlay(tenant, edition, "CC6.1", Uuid.CreateVersion4());

        // Act
        var otherCriterion = new CriteriaTextOverlay(tenant, edition, "CC6.2");
        var otherEdition = new CriteriaTextOverlay(tenant, Uuid.CreateVersion4(), "CC6.1");
        var otherTenant = new CriteriaTextOverlay(Uuid.CreateVersion4(), edition, "CC6.1");

        // Assert
        Assert.Equal(first.Id, sameWithOperation.Id);
        Assert.Equal(first.Stream, sameWithOperation.Stream);
        Assert.Equal(CriteriaTextOverlay.IdentityOf(tenant, edition, "CC6.1"), first.Id);
        Assert.NotEqual(first.Id, otherCriterion.Id);
        Assert.NotEqual(first.Id, otherEdition.Id);
        Assert.NotEqual(first.Id, otherTenant.Id);
    }

    [Fact]
    public void ShouldRaiseNextRevisionGivenExactExpectedRevision()
    {
        // Arrange
        var owner = Open();
        var scenario = new AggregateScenario<CriteriaTextOverlay>(owner);

        // Act
        var first = Set(owner, 0);
        var second = Set(owner, 1);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, scenario.PendingEvents.Count);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(2, owner.Latest!.Revision);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenStaleOrFutureExpectedRevision()
    {
        // Arrange
        var owner = Open();
        var scenario = new AggregateScenario<CriteriaTextOverlay>(owner);
        Assert.True(Set(owner, 0).IsSuccess);

        // Act
        var stale = Set(owner, 0);
        var future = Set(owner, 5);
        var prepared = owner.PrepareSet(0);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.False(future.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, future.Error!.Kind);
        Assert.False(prepared.IsSuccess);
        Assert.Single(scenario.PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenMissingExpectedRevisionForeignOwnerOrOtherCommandKind()
    {
        // Arrange
        var owner = Open();
        var scenario = new AggregateScenario<CriteriaTextOverlay>(owner);
        var other = new CriteriaTextOverlay(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "CC6.2");
        var unguarded = NativeFacts.Root(owner, Uuid.CreateVersion4(), CommandKind, null);
        var foreign = NativeFacts.Root(other, Uuid.CreateVersion4(), CommandKind, 0);
        var otherKind = NativeFacts.Root(owner, Uuid.CreateVersion4(), "RemoveCriteriaTextOverlay", 0);

        // Act
        var missing = owner.Set(unguarded, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        var wrongOwner = owner.Set(foreign, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        var wrongKind = owner.Set(otherKind, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(missing.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, missing.Error!.Kind);
        Assert.False(wrongOwner.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, wrongOwner.Error!.Kind);
        Assert.False(wrongKind.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, wrongKind.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldReplayLineGivenValidRaisedHistoryAndAcceptNextRevision()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var edition = Uuid.CreateVersion4();
        var seed = new CriteriaTextOverlay(tenant, edition, "CC6.1");
        Assert.True(Set(seed, 0).IsSuccess);
        Assert.True(Set(seed, 1).IsSuccess);
        var history = NativeFacts.Raised(seed);

        // Act
        var replayed = NativeFacts.Replay(new CriteriaTextOverlay(tenant, edition, "CC6.1"), history);
        var revision = replayed.Revision;
        var latest = replayed.Latest;
        var stale = Set(replayed, 1);
        var next = Set(replayed, 2);

        // Assert
        Assert.Equal(2, history.Length);
        Assert.Equal(2, revision);
        Assert.Equal(2, latest!.Revision);
        Assert.Equal("CC6.1", latest.Identifier);
        Assert.False(stale.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.True(next.IsSuccess);
        Assert.Equal(3, replayed.Revision);
    }

    static CriteriaTextOverlay Open() =>
        new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "CC6.1");

    static Result Set(CriteriaTextOverlay owner, long expectedRevision) => owner.Set(
        NativeFacts.Root(owner, Uuid.CreateVersion4(), CommandKind, expectedRevision),
        NativeFacts.Ref(CriteriaTextOverlay.ContentSchema), NativeFacts.Ref(CriteriaTextOverlay.RegistrationSchema),
        NativeFacts.Ref(CriteriaTextOverlay.RegistrationSchema));
}
