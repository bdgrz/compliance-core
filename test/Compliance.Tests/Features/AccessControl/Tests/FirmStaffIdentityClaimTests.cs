using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

/// <summary>Direct decisions on the permanent canonical-user claim.</summary>
public sealed class FirmStaffIdentityClaimTests
{
    [Fact]
    public void ShouldKeepClaimIdentityStableGivenCanonicalUser()
    {
        // Arrange
        var user = Uuid.CreateVersion4();

        // Act
        var first = new FirmStaffIdentityClaim(user);
        var same = new FirmStaffIdentityClaim(user, Uuid.CreateVersion4());
        var other = new FirmStaffIdentityClaim(Uuid.CreateVersion4());

        // Assert
        Assert.Equal(first.Stream, same.Stream);
        Assert.Equal(first.Id, same.Id);
        Assert.NotEqual(first.Stream, other.Stream);
        Assert.Equal(FirmStaffIdentityClaim.Area, first.Stream.Area);
    }

    [Fact]
    public void ShouldRefuseClaimWithoutFactGivenForeignOwnerOrEmptyStaffIdentity()
    {
        // Arrange
        var owner = new FirmStaffIdentityClaim(Uuid.CreateVersion4());
        var scenario = new AggregateScenario<FirmStaffIdentityClaim>(owner);
        var operation = Child(owner, Uuid.CreateVersion4());
        var foreign = operation with { Owner = new FirmStaffIdentityClaim(Uuid.CreateVersion4()).Stream };
        var empty = operation with { RootOperationId = Uuid.Empty };

        // Act
        var foreignResult = owner.Claim(foreign, NativeFacts.Ref());
        var emptyResult = owner.Claim(empty, NativeFacts.Ref());

        // Assert
        Assert.False(foreignResult.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, foreignResult.Error!.Kind);
        Assert.False(emptyResult.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, emptyResult.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.False(owner.IsClaimed);
    }

    [Fact]
    public void ShouldPreserveClaimAndRefuseCompetingClaimGivenValidReplay()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var staff = Uuid.CreateVersion4();
        var seed = new FirmStaffIdentityClaim(user);
        var operation = Child(seed, staff);
        Assert.True(seed.Claim(operation, NativeFacts.Ref()).IsSuccess);
        var facts = NativeFacts.Raised(seed);
        var owner = NativeFacts.Replay(new FirmStaffIdentityClaim(user), facts);
        var scenario = new AggregateScenario<FirmStaffIdentityClaim>(owner);

        // Act
        var result = owner.Claim(Child(owner, Uuid.CreateVersion4()), NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.False(result.Error!.IsTransient);
        Assert.Empty(scenario.PendingEvents);
        Assert.True(owner.IsClaimed);
        Assert.Equal(staff, owner.ClaimedByStaffMemberId);
        Assert.IsType<FirmStaffIdentityClaimed>(Assert.Single(facts));
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenEmptyCanonicalUserIdentity()
    {
        // Arrange
        var owner = new FirmStaffIdentityClaim(Uuid.Empty);
        var scenario = new AggregateScenario<FirmStaffIdentityClaim>(owner);
        var operation = Child(owner, Uuid.CreateVersion4());

        // Act
        var result = owner.Claim(operation, NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.False(owner.IsClaimed);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenInvalidRegistrationChildProvenance()
    {
        // Arrange
        var owner = new FirmStaffIdentityClaim(Uuid.CreateVersion4());
        var scenario = new AggregateScenario<FirmStaffIdentityClaim>(owner);
        var operation = Child(owner, Uuid.CreateVersion4());
        var foreign = new FirmStaffIdentityClaim(Uuid.CreateVersion4()).Stream;
        var invalid = new[]
        {
            operation with { LogicalId = Uuid.Empty },
            operation with { CommandKind = "OtherCommand" },
            operation with { Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Tester") },
            operation with { CausationEventId = null },
            operation with { FirstOwner = foreign },
            operation with { AffectedOwner = foreign },
            operation with { ExpectedRevisionOwner = foreign },
            operation with { OriginalExpectedRevision = 1 },
        };
        var intent = NativeFacts.Ref();

        // Act
        var results = invalid.Select(value => owner.Claim(value, intent)).ToArray();

        // Assert
        Assert.All(results, result =>
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        });
        Assert.Empty(scenario.PendingEvents);
        Assert.False(owner.IsClaimed);
    }

    static OperationProvenance Child(FirmStaffIdentityClaim owner, Uuid staffId) =>
        new(Uuid.CreateVersion4(), staffId, Uuid.CreateVersion4(), nameof(ClaimFirmStaffIdentity),
            ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Tester"), owner.Stream, "initial", 0,
            new string('b', 64), NativeFacts.Now, owner.Stream, owner.Stream, new FirmStaffRecord(staffId).Stream);
}
