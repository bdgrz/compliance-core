using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Services;
namespace Bdgrz.Compliance.Tests.Features.Providers.Tests;

public sealed class ProviderNameClaimTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    static readonly ImmutableContentRef Intent = new(Uuid.CreateVersion4(), new string('a', 64), 1,
        "application/json", "named-record-change-intent.v1");

    [Fact]
    public void ShouldAddressSameStreamGivenSameTenantAndName()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();

        // Act
        var first = ProviderNameClaim.AddressOf(tenantId, "HQ");
        var second = ProviderNameClaim.AddressOf(tenantId, "HQ");

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(tenantId.ToString(), first.Realm);
        Assert.Equal(ProviderNameClaim.Area, first.Area);
    }

    [Fact]
    public void ShouldAddressDifferentStreamsGivenDifferentNameOrTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var baseline = ProviderNameClaim.AddressOf(tenantId, "HQ");

        // Act
        var otherName = ProviderNameClaim.AddressOf(tenantId, "ACME");
        var otherTenant = ProviderNameClaim.AddressOf(Uuid.CreateVersion4(), "HQ");

        // Assert
        Assert.NotEqual(baseline.Resource, otherName.Resource);
        Assert.NotEqual(baseline.Resource, otherTenant.Resource);
    }

    [Fact]
    public void ShouldMatchDescriptorStreamGivenNormalizedName()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();

        // Act
        var address = ProviderNameClaim.AddressOf(tenantId, "HQ");

        // Assert
        Assert.Equal(ProviderNames.Descriptor.ClaimStream(tenantId, null, "HQ"), address);
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
        var address = () => ProviderNameClaim.AddressOf(tenantId, name);

        // Assert
        Assert.ThrowsAny<ArgumentException>(address);
    }

    [Fact]
    public void ShouldRefuseConstructionGivenStreamOfAnotherTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var foreign = ProviderNameClaim.AddressOf(Uuid.CreateVersion4(), "HQ");

        // Act
        var open = () => new ProviderNameClaim(tenantId, foreign);

        // Assert
        Assert.Throws<ArgumentException>(open);
    }

    [Fact]
    public void ShouldAppendClaimedFactGivenFreeNameAndProviderInTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var holder = HolderOf(tenantId);

        // Act
        var result = claim.Claim(Operation(claim, "ClaimProviderName"), holder, 0, 1, Intent);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, PendingCount(claim));
        Assert.Equal(holder, claim.Holder);
        Assert.Equal(1, claim.HeldGeneration);
    }

    [Fact]
    public void ShouldRejectHolderGivenForeignTenantProviderOnClaim()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var foreign = HolderOf(Uuid.CreateVersion4());

        // Act
        var result = claim.Claim(Operation(claim, "ClaimProviderName"), foreign, 0, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, PendingCount(claim));
    }

    [Fact]
    public void ShouldRejectHolderGivenNonProviderStreamOnClaim()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var process = new EventStreamAddress(tenantId.ToString(), "operational-processes", Uuid.CreateVersion4().ToString());

        // Act
        var result = claim.Claim(Operation(claim, "ClaimProviderName"), process, 0, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, PendingCount(claim));
    }

    [Fact]
    public void ShouldRejectHolderGivenForeignTenantProviderOnRelease()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var holder = HolderOf(tenantId);
        Assert.True(claim.Claim(Operation(claim, "ClaimProviderName"), holder, 0, 1, Intent).IsSuccess);
        var pendingCount = PendingCount(claim);

        // Act
        var result = claim.Release(Operation(claim, "ReleaseProviderName"), HolderOf(Uuid.CreateVersion4()), 1, 1,
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
        var other = NewClaim(tenantId, "ACME");

        // Act
        var result = claim.Claim(Operation(other, "ClaimProviderName"), HolderOf(tenantId), 0, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, PendingCount(claim));
    }

    [Fact]
    public void ShouldRefuseClaimGivenNameHeldByAnotherProvider()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        Assert.True(claim.Claim(Operation(claim, "ClaimProviderName"), HolderOf(tenantId), 0, 1, Intent).IsSuccess);
        var pendingCount = PendingCount(claim);

        // Act
        var result = claim.Claim(Operation(claim, "ClaimProviderName"), HolderOf(tenantId), 0, 1, Intent);

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
        Assert.True(claim.Claim(Operation(claim, "ClaimProviderName"), holder, 0, 1, Intent).IsSuccess);

        // Act
        var result = claim.Claim(Operation(claim, "ClaimProviderName"), holder, 1, 2, Intent);

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
        Assert.True(claim.Claim(Operation(claim, "ClaimProviderName"), holder, 0, 2, Intent).IsSuccess);
        var pendingCount = PendingCount(claim);

        // Act
        var result = claim.Claim(Operation(claim, "ClaimProviderName"), holder, 1, 1, Intent);

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
        var result = claim.Release(Operation(claim, "ReleaseProviderName"), HolderOf(tenantId), 0, 1, Intent);

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
        Assert.True(claim.Claim(Operation(claim, "ClaimProviderName"), holder, 0, 1, Intent).IsSuccess);
        Assert.True(claim.Claim(Operation(claim, "ClaimProviderName"), holder, 1, 2, Intent).IsSuccess);
        var pendingCount = PendingCount(claim);

        // Act
        var result = claim.Release(Operation(claim, "ReleaseProviderName"), holder, 2, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(pendingCount, PendingCount(claim));
        Assert.Equal(holder, claim.Holder);
    }

    [Fact]
    public void ShouldFreeNameForAnotherProviderGivenExactRelease()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var holder = HolderOf(tenantId);
        var next = HolderOf(tenantId);
        Assert.True(claim.Claim(Operation(claim, "ClaimProviderName"), holder, 0, 1, Intent).IsSuccess);

        // Act
        var released = claim.Release(Operation(claim, "ReleaseProviderName"), holder, 1, 1, Intent);
        var reclaimed = claim.Claim(Operation(claim, "ClaimProviderName"), next, 0, 1, Intent);

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
        var address = ProviderNameClaim.AddressOf(tenantId, "HQ");
        var probe = new ProviderNameClaim(tenantId, address);
        var holder = HolderOf(tenantId);
        var facts = new DomainEvent[]
        {
            Seed(probe, new ProviderNameClaimed(holder, 0, 1, Intent, null, Operation(probe, "ClaimProviderName"),
                null, null), 1),
            Seed(probe, new ProviderNameClaimed(holder, 1, 2, Intent, null, Operation(probe, "ClaimProviderName"),
                null, null), 2),
        };

        // Act
        var claim = new AggregateScenario<ProviderNameClaim>(new ProviderNameClaim(tenantId, address)).Given(facts)
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
        var address = ProviderNameClaim.AddressOf(tenantId, "HQ");
        var probe = new ProviderNameClaim(tenantId, address);
        var holder = HolderOf(tenantId);
        var facts = new DomainEvent[]
        {
            Seed(probe, new ProviderNameClaimed(holder, 0, 1, Intent, null, Operation(probe, "ClaimProviderName"),
                null, null), 1),
            Seed(probe, new ProviderNameReleased(holder, 1, 1, Intent, null,
                Operation(probe, "ReleaseProviderName"), null, null), 2),
        };

        // Act
        var claim = new AggregateScenario<ProviderNameClaim>(new ProviderNameClaim(tenantId, address)).Given(facts)
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
        var address = ProviderNameClaim.AddressOf(tenantId, "HQ");
        var probe = new ProviderNameClaim(tenantId, address);
        var holder = HolderOf(tenantId);
        var logicalId = Uuid.CreateVersion4();
        var claimOperation = Operation(probe, "ClaimProviderName", logicalId);
        var facts = new DomainEvent[]
        {
            Seed(probe, new ProviderNameClaimed(holder, 0, 1, Intent, null, claimOperation, null, null), 1),
            Seed(probe, new ProviderNameReleased(holder, 1, 1, Intent, null,
                Operation(probe, "ReleaseProviderName"), null, null), 2),
        };
        var claim = new AggregateScenario<ProviderNameClaim>(new ProviderNameClaim(tenantId, address, logicalId))
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
        var address = ProviderNameClaim.AddressOf(tenantId, "HQ");
        var probe = new ProviderNameClaim(tenantId, address);
        var holder = HolderOf(tenantId);
        var logicalId = Uuid.CreateVersion4();
        var claimOperation = Operation(probe, "ClaimProviderName", logicalId);
        var claim = new AggregateScenario<ProviderNameClaim>(new ProviderNameClaim(tenantId, address, logicalId))
            .Given(Seed(probe, new ProviderNameClaimed(holder, 0, 1, Intent, null, claimOperation, null, null), 1))
            .Aggregate;
        var changed = claimOperation with { AuthoredIntentSha256 = new string('c', 64) };

        // Act
        var decision = claim.Acceptance.Decide(AcceptedOperationBinding.Of(changed));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Conflict, decision);
    }


    [Fact]
    public void ShouldRejectHolderGivenNonUuidProviderResourceOnClaim()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var holder = new EventStreamAddress(tenantId.ToString(), Provider.Area, "not-a-uuid");

        // Act
        var result = claim.Claim(Operation(claim, "ClaimProviderName"), holder, 0, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, PendingCount(claim));
    }

    [Fact]
    public void ShouldRefuseReleaseGivenSameHolderAfterRelease()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = NewClaim(tenantId);
        var holder = HolderOf(tenantId);
        Assert.True(claim.Claim(Operation(claim, "ClaimProviderName"), holder, 0, 1, Intent).IsSuccess);
        Assert.True(claim.Release(Operation(claim, "ReleaseProviderName"), holder, 1, 1, Intent).IsSuccess);
        var pendingCount = PendingCount(claim);

        // Act
        var result = claim.Release(Operation(claim, "ReleaseProviderName"), holder, 2, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(pendingCount, PendingCount(claim));
    }

    static ProviderNameClaim NewClaim(Uuid tenantId, string name = "HQ") =>
        new(tenantId, ProviderNameClaim.AddressOf(tenantId, name));

    static EventStreamAddress HolderOf(Uuid tenantId) => new Provider(tenantId, Uuid.CreateVersion4()).Stream;

    static int PendingCount(ProviderNameClaim claim) =>
        new AggregateScenario<ProviderNameClaim>(claim).PendingEvents.Count;

    static DomainEvent Seed(ProviderNameClaim claim, DomainEvent fact, ulong version) =>
        DomainEventSeed.Attach(fact, claim.Id, version);

    static OperationProvenance Operation(ProviderNameClaim claim, string commandKind, Uuid? logicalId = null)
    {
        var id = logicalId ?? Uuid.CreateVersion4();
        var actor = ActorReference.ForSystemProcess("name-claim-test", "Name claim test");
        return new OperationProvenance(id, id, null, commandKind, actor,
            claim.Stream, "initial", null, new string('b', 64), Now, claim.Stream, claim.Stream);
    }
}
