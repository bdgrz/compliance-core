using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;

public sealed class LocationNameClaimTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    static readonly ImmutableContentRef Intent = new(Uuid.CreateVersion4(), new string('a', 64), 1,
        "application/json", "named-record-change-intent.v1");

    [Fact]
    public void ShouldAddressSameStreamGivenSameTenantKindAndName()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();

        // Act
        var first = LocationNameClaim.AddressOf(tenantId, "physical_site", "HQ");
        var second = LocationNameClaim.AddressOf(tenantId, "physical_site", "HQ");

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(tenantId.ToString(), first.Realm);
        Assert.Equal(LocationNameClaim.Area, first.Area);
    }

    [Fact]
    public void ShouldAddressDifferentStreamsGivenDifferentKindOrTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var baseline = LocationNameClaim.AddressOf(tenantId, "physical_site", "HQ");

        // Act
        var otherKind = LocationNameClaim.AddressOf(tenantId, "hosting_region", "HQ");
        var otherTenant = LocationNameClaim.AddressOf(Uuid.CreateVersion4(), "physical_site", "HQ");

        // Assert
        Assert.NotEqual(baseline.Resource, otherKind.Resource);
        Assert.NotEqual(baseline.Resource, otherTenant.Resource);
    }

    [Fact]
    public void ShouldMatchDescriptorStreamGivenNormalizedName()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();

        // Act
        var address = LocationNameClaim.AddressOf(tenantId, "physical_site", "HQ");

        // Assert
        Assert.Equal(LocationNames.Descriptor.ClaimStream(tenantId, "physical_site", "HQ"), address);
    }

    [Theory]
    [InlineData("hq")]
    [InlineData(" HQ ")]
    [InlineData("")]
    public void ShouldRefuseAddressGivenNameThatIsNotNormalized(string name)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();

        // Act
        var address = () => LocationNameClaim.AddressOf(tenantId, "physical_site", name);

        // Assert
        Assert.ThrowsAny<ArgumentException>(address);
    }

    [Fact]
    public void ShouldRefuseConstructionGivenStreamOfAnotherTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var foreign = LocationNameClaim.AddressOf(Uuid.CreateVersion4(), "physical_site", "HQ");

        // Act
        var open = () => new LocationNameClaim(tenantId, foreign);

        // Assert
        Assert.Throws<ArgumentException>(open);
    }

    [Fact]
    public void ShouldAppendClaimedFactGivenFreeNameAndLocalLocation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var holder = HolderOf(tenantId);

        // Act
        var result = claim.Claim(Operation(claim, "ClaimLocationName"), holder, 0, 1, Intent);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, PendingCount(claim));
        Assert.Equal(holder, claim.Holder);
        Assert.Equal(1, claim.HeldGeneration);
    }

    [Fact]
    public void ShouldRejectHolderGivenForeignTenantLocationOnClaim()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var foreign = HolderOf(Uuid.CreateVersion4());

        // Act
        var result = claim.Claim(Operation(claim, "ClaimLocationName"), foreign, 0, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, PendingCount(claim));
    }

    [Fact]
    public void ShouldRejectHolderGivenNonLocationStreamOnClaim()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var process = new OperationalProcess(tenantId, Uuid.CreateVersion4()).Stream;

        // Act
        var result = claim.Claim(Operation(claim, "ClaimLocationName"), process, 0, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, PendingCount(claim));
    }

    [Fact]
    public void ShouldRejectHolderGivenForeignTenantLocationOnRelease()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var holder = HolderOf(tenantId);
        Assert.True(claim.Claim(Operation(claim, "ClaimLocationName"), holder, 0, 1, Intent).IsSuccess);
        var pendingCount = PendingCount(claim);

        // Act
        var result = claim.Release(Operation(claim, "ReleaseLocationName"), HolderOf(Uuid.CreateVersion4()), 1, 1,
            Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(pendingCount, PendingCount(claim));
        Assert.Equal(holder, claim.Holder);
    }

    [Fact]
    public void ShouldRefuseOperationGivenAnotherOwnerStream()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var other = NewClaim(tenantId, "hosting_region");

        // Act
        var result = claim.Claim(Operation(other, "ClaimLocationName"), HolderOf(tenantId), 0, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, PendingCount(claim));
    }

    [Fact]
    public void ShouldRefuseClaimGivenNameHeldByAnotherLocation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        Assert.True(claim.Claim(Operation(claim, "ClaimLocationName"), HolderOf(tenantId), 0, 1, Intent).IsSuccess);
        var pendingCount = PendingCount(claim);

        // Act
        var result = claim.Claim(Operation(claim, "ClaimLocationName"), HolderOf(tenantId), 0, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(pendingCount, PendingCount(claim));
    }

    [Fact]
    public void ShouldRepinClaimGivenSameHolderAndLaterGeneration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var holder = HolderOf(tenantId);
        Assert.True(claim.Claim(Operation(claim, "ClaimLocationName"), holder, 0, 1, Intent).IsSuccess);

        // Act
        var result = claim.Claim(Operation(claim, "ClaimLocationName"), holder, 1, 2, Intent);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, claim.HeldGeneration);
        Assert.Equal(2, PendingCount(claim));
    }

    [Fact]
    public void ShouldRefuseClaimGivenSameHolderAndSupersededGeneration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var holder = HolderOf(tenantId);
        Assert.True(claim.Claim(Operation(claim, "ClaimLocationName"), holder, 0, 2, Intent).IsSuccess);
        var pendingCount = PendingCount(claim);

        // Act
        var result = claim.Claim(Operation(claim, "ClaimLocationName"), holder, 1, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(pendingCount, PendingCount(claim));
        Assert.Equal(2, claim.HeldGeneration);
    }

    [Fact]
    public void ShouldRefuseReleaseGivenNameThatIsFree()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);

        // Act
        var result = claim.Release(Operation(claim, "ReleaseLocationName"), HolderOf(tenantId), 0, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, PendingCount(claim));
    }

    [Fact]
    public void ShouldRefuseReleaseGivenDelayedGenerationAfterRepin()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var holder = HolderOf(tenantId);
        Assert.True(claim.Claim(Operation(claim, "ClaimLocationName"), holder, 0, 1, Intent).IsSuccess);
        Assert.True(claim.Claim(Operation(claim, "ClaimLocationName"), holder, 1, 2, Intent).IsSuccess);
        var pendingCount = PendingCount(claim);

        // Act
        var result = claim.Release(Operation(claim, "ReleaseLocationName"), holder, 2, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(pendingCount, PendingCount(claim));
        Assert.Equal(holder, claim.Holder);
    }

    [Fact]
    public void ShouldFreeNameForAnotherLocationGivenExactRelease()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var holder = HolderOf(tenantId);
        var next = HolderOf(tenantId);
        Assert.True(claim.Claim(Operation(claim, "ClaimLocationName"), holder, 0, 1, Intent).IsSuccess);

        // Act
        var released = claim.Release(Operation(claim, "ReleaseLocationName"), holder, 1, 1, Intent);
        var reclaimed = claim.Claim(Operation(claim, "ClaimLocationName"), next, 0, 1, Intent);

        // Assert
        Assert.True(released.IsSuccess);
        Assert.True(reclaimed.IsSuccess);
        Assert.Equal(next, claim.Holder);
    }

    [Fact]
    public void ShouldRestoreHolderGivenValidClaimHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var address = LocationNameClaim.AddressOf(tenantId, "physical_site", "HQ");
        var probe = new LocationNameClaim(tenantId, address);
        var holder = HolderOf(tenantId);
        var facts = new DomainEvent[]
        {
            Seed(probe, new LocationNameClaimed(holder, 0, 1, Intent, null, Operation(probe, "ClaimLocationName"),
                null, null), 1),
            Seed(probe, new LocationNameClaimed(holder, 1, 2, Intent, null, Operation(probe, "ClaimLocationName"),
                null, null), 2),
        };

        // Act
        var claim = new AggregateScenario<LocationNameClaim>(new LocationNameClaim(tenantId, address)).Given(facts)
            .Aggregate;

        // Assert
        Assert.Equal(holder, claim.Holder);
        Assert.Equal(2, claim.HeldGeneration);
    }

    [Fact]
    public void ShouldRestoreFreeNameGivenClaimThenRelease()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var address = LocationNameClaim.AddressOf(tenantId, "physical_site", "HQ");
        var probe = new LocationNameClaim(tenantId, address);
        var holder = HolderOf(tenantId);
        var facts = new DomainEvent[]
        {
            Seed(probe, new LocationNameClaimed(holder, 0, 1, Intent, null, Operation(probe, "ClaimLocationName"),
                null, null), 1),
            Seed(probe, new LocationNameReleased(holder, 1, 1, Intent, null,
                Operation(probe, "ReleaseLocationName"), null, null), 2),
        };

        // Act
        var claim = new AggregateScenario<LocationNameClaim>(new LocationNameClaim(tenantId, address)).Given(facts)
            .Aggregate;

        // Assert
        Assert.Null(claim.Holder);
        Assert.Equal(0, claim.HeldGeneration);
    }

    [Fact]
    public void ShouldReplayCommittedClaimGivenExactRetryAfterLaterFacts()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var address = LocationNameClaim.AddressOf(tenantId, "physical_site", "HQ");
        var probe = new LocationNameClaim(tenantId, address);
        var holder = HolderOf(tenantId);
        var logicalId = Uuid.CreateVersion4();
        var claimOperation = Operation(probe, "ClaimLocationName", logicalId);
        var facts = new DomainEvent[]
        {
            Seed(probe, new LocationNameClaimed(holder, 0, 1, Intent, null, claimOperation, null, null), 1),
            Seed(probe, new LocationNameReleased(holder, 1, 1, Intent, null,
                Operation(probe, "ReleaseLocationName"), null, null), 2),
        };
        var claim = new AggregateScenario<LocationNameClaim>(new LocationNameClaim(tenantId, address, logicalId))
            .Given(facts).Aggregate;

        // Act
        var decision = claim.Acceptance.Decide(AcceptedOperationBinding.Of(claimOperation));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, decision);
        Assert.Null(claim.Holder);
    }

    [Fact]
    public void ShouldConflictGivenSameLogicalOperationWithChangedIntent()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var address = LocationNameClaim.AddressOf(tenantId, "physical_site", "HQ");
        var probe = new LocationNameClaim(tenantId, address);
        var holder = HolderOf(tenantId);
        var logicalId = Uuid.CreateVersion4();
        var claimOperation = Operation(probe, "ClaimLocationName", logicalId);
        var claim = new AggregateScenario<LocationNameClaim>(new LocationNameClaim(tenantId, address, logicalId))
            .Given(Seed(probe, new LocationNameClaimed(holder, 0, 1, Intent, null, claimOperation, null, null), 1))
            .Aggregate;
        var changed = claimOperation with { AuthoredIntentSha256 = new string('c', 64) };

        // Act
        var decision = claim.Acceptance.Decide(AcceptedOperationBinding.Of(changed));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Conflict, decision);
    }

    static LocationNameClaim NewClaim(Uuid tenantId, string kind = "physical_site") =>
        new(tenantId, LocationNameClaim.AddressOf(tenantId, kind, "HQ"));

    static EventStreamAddress HolderOf(Uuid tenantId) => new Location(tenantId, Uuid.CreateVersion4()).Stream;

    static int PendingCount(LocationNameClaim claim) =>
        new AggregateScenario<LocationNameClaim>(claim).PendingEvents.Count;

    static DomainEvent Seed(LocationNameClaim claim, DomainEvent fact, ulong version) =>
        DomainEventSeed.Attach(fact, claim.Id, version);

    static OperationProvenance Operation(LocationNameClaim claim, string commandKind, Uuid? logicalId = null)
    {
        var id = logicalId ?? Uuid.CreateVersion4();
        var actor = ActorReference.ForSystemProcess("name-claim-test", "Name claim test");
        return new OperationProvenance(id, id, null, commandKind, actor,
            claim.Stream, "initial", null, new string('b', 64), Now, claim.Stream, claim.Stream);
    }
}
