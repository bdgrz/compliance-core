using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

/// <summary>Owner-level decisions for the permanent signed assertion identity claim.</summary>
public sealed class ProfessionalAssertionIdentityClaimTests
{
    [Fact]
    public void ShouldKeepStreamIdentityStableGivenSelectedOperationChanges()
    {
        // Arrange
        var assertion = Uuid.CreateVersion4();

        // Act
        var first = new ProfessionalAssertionIdentityClaim(assertion);
        var selected = new ProfessionalAssertionIdentityClaim(assertion, Uuid.CreateVersion4());
        var other = new ProfessionalAssertionIdentityClaim(Uuid.CreateVersion4());

        // Assert
        Assert.Equal(first.Stream, selected.Stream);
        Assert.Equal(first.Id, selected.Id);
        Assert.NotEqual(first.Stream, other.Stream);
        Assert.Equal(ProfessionalAssertionIdentityClaim.Area, first.Stream.Area);
    }

    [Fact]
    public void ShouldRecordOneClaimGivenFreshAssertion()
    {
        // Arrange
        var assertion = Uuid.CreateVersion4();
        var owner = new ProfessionalAssertionIdentityClaim(assertion);
        var scenario = new AggregateScenario<ProfessionalAssertionIdentityClaim>(owner);
        var intent = NativeFacts.Ref();
        var operation = Provenance(owner, Uuid.CreateVersion4());

        // Act
        var result = owner.Claim(operation, intent);

        // Assert
        Assert.True(result.IsSuccess);
        var fact = Assert.IsType<ProfessionalAssertionIdentityClaimed>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(owner.Stream, fact.Owner);
        Assert.Equal(intent, fact.Intent);
        Assert.Equal(assertion, fact.Operation.RootOperationId);
        Assert.Equal(1L, fact.Revision);
        Assert.True(owner.IsClaimed);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenForeignOwnerOrAssertion()
    {
        // Arrange
        var owner = new ProfessionalAssertionIdentityClaim(Uuid.CreateVersion4());
        var scenario = new AggregateScenario<ProfessionalAssertionIdentityClaim>(owner);
        var operation = Provenance(owner, Uuid.CreateVersion4());
        var foreignOwner = new ProfessionalAssertionIdentityClaim(Uuid.CreateVersion4());
        var intent = NativeFacts.Ref();

        // Act
        var foreign = owner.Claim(operation with { Owner = foreignOwner.Stream }, intent);
        var wrongAssertion = owner.Claim(operation with { RootOperationId = Uuid.CreateVersion4() }, intent);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, foreign.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, wrongAssertion.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.False(owner.IsClaimed);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenEmptyAssertionIdentity()
    {
        // Arrange
        var owner = new ProfessionalAssertionIdentityClaim(Uuid.Empty);
        var scenario = new AggregateScenario<ProfessionalAssertionIdentityClaim>(owner);
        var operation = Provenance(owner, Uuid.CreateVersion4());

        // Act
        var result = owner.Claim(operation, NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.False(owner.IsClaimed);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenEmptyLogicalIdentity()
    {
        // Arrange
        var owner = new ProfessionalAssertionIdentityClaim(Uuid.CreateVersion4());
        var scenario = new AggregateScenario<ProfessionalAssertionIdentityClaim>(owner);
        var operation = Provenance(owner, Uuid.Empty);

        // Act
        var result = owner.Claim(operation, NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.False(owner.IsClaimed);
    }

    [Fact]
    public void ShouldPreserveClaimAndRecognizeRetryGivenReplayedHistory()
    {
        // Arrange
        var assertion = Uuid.CreateVersion4();
        var logicalId = Uuid.CreateVersion4();
        var seed = new ProfessionalAssertionIdentityClaim(assertion);
        var operation = Provenance(seed, logicalId);
        var intent = NativeFacts.Ref();
        Assert.True(seed.Claim(operation, intent).IsSuccess);
        var owner = NativeFacts.Replay(new ProfessionalAssertionIdentityClaim(assertion, logicalId),
            NativeFacts.Raised(seed));
        var scenario = new AggregateScenario<ProfessionalAssertionIdentityClaim>(owner);
        var changed = operation with { AuthoredIntentSha256 = new string('c', 64) };

        // Act
        var retry = owner.Acceptance.Decide(AcceptedOperationBinding.Of(operation));
        var changedRetry = owner.Acceptance.Decide(AcceptedOperationBinding.Of(changed));
        var competing = owner.Claim(Provenance(owner, Uuid.CreateVersion4()), NativeFacts.Ref());

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, retry);
        Assert.Equal(NativeAcceptanceDecision.Conflict, changedRetry);
        Assert.False(competing.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, competing.Error!.Kind);
        Assert.False(competing.Error.IsTransient);
        Assert.Equal(intent, owner.Claimed!.Intent);
        Assert.Empty(scenario.PendingEvents);
    }

    static OperationProvenance Provenance(ProfessionalAssertionIdentityClaim owner, Uuid logicalId) =>
        new(logicalId, owner.AssertionId, null, "ClaimProfessionalAssertionIdentity",
            ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Tester"), owner.Stream, "initial", 0,
            new string('b', 64), NativeFacts.Now, owner.Stream, owner.Stream);
}
