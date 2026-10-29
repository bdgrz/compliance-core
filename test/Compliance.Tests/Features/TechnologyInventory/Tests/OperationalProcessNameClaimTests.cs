using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Processes;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;

public sealed class OperationalProcessNameClaimTests
{
    const string ClaimKind = "ClaimOperationalProcessName";
    const string ReleaseKind = "ReleaseOperationalProcessName";

    static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Actor = ActorReference.ForSystemProcess("name-claim-test", "Name claim test");
    static readonly ImmutableContentRef Intent = new(Uuid.CreateVersion4(), new string('a', 64), 1,
        "application/json", "named-record-change-intent.v1");
    static readonly ImmutableContentRef OtherIntent = new(Uuid.CreateVersion4(), new string('c', 64), 1,
        "application/json", "named-record-change-intent.v1");

    [Fact]
    public void ShouldKeepOneStreamPerNameGivenSameTenantAndNormalizedName()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();

        // Act
        var first = Open(tenantId, "backup process");
        var second = Open(tenantId, "backup process");
        var otherName = Open(tenantId, "restore process");
        var otherTenant = Open(Uuid.CreateVersion4(), "backup process");

        // Assert
        Assert.Equal(first.Stream, second.Stream);
        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(first.Stream, otherName.Stream);
        Assert.NotEqual(first.Stream, otherTenant.Stream);
        Assert.Equal(tenantId.ToString(), first.Stream.Realm);
        Assert.Equal(OperationalProcessNameClaim.Area, first.Stream.Area);
    }

    [Fact]
    public void ShouldRejectStreamGivenAnotherTenantOrAnotherArea()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var address = OperationalProcessNameClaim.AddressOf(tenantId, "backup process");
        var otherTenant = new EventStreamAddress(Uuid.CreateVersion4().ToString(), address.Area, address.Resource);
        var otherArea = new EventStreamAddress(address.Realm, "provider-name-claims", address.Resource);

        // Act
        var foreignTenant = Record.Exception(() => new OperationalProcessNameClaim(tenantId, otherTenant));
        var foreignArea = Record.Exception(() => new OperationalProcessNameClaim(tenantId, otherArea));

        // Assert
        Assert.IsType<ArgumentException>(foreignTenant);
        Assert.IsType<ArgumentException>(foreignArea);
    }

    [Fact]
    public void ShouldHoldNameAndRaiseOneFactGivenFreeNameAndProcessHolder()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = Open(tenantId, "backup process");
        var holder = ProcessStream(tenantId);
        var operation = Operation(claim, ClaimKind);

        // Act
        var result = claim.Claim(operation, holder, 4, 2, Intent);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(holder, claim.Holder);
        Assert.Equal(2, claim.HeldGeneration);
        var claimed = Assert.IsType<OperationalProcessNameClaimed>(Assert.Single(Pending(claim)));
        Assert.Equal(holder, claimed.Owner);
        Assert.Equal(4, claimed.Revision);
        Assert.Equal(2, claimed.Generation);
        Assert.Equal(Intent, claimed.Intent);
        Assert.Equal(operation, claimed.Operation);
    }

    [Fact]
    public void ShouldRejectForeignTenantHolderGivenOperationalProcessNameClaim()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = Open(tenantId, "backup process");
        var foreignHolder = ProcessStream(Uuid.CreateVersion4());

        // Act
        var result = claim.Claim(Operation(claim, ClaimKind), foreignHolder, 0, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Pending(claim));
        Assert.Null(claim.Holder);
    }

    [Fact]
    public void ShouldRejectHolderThatIsNotAProcessGivenOperationalProcessNameClaim()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = Open(tenantId, "backup process");
        var location = new EventStreamAddress(tenantId.ToString(), "inventory-registers",
            $"location-{Uuid.CreateVersion4()}");
        var emptyProcess = new EventStreamAddress(tenantId.ToString(), "inventory-registers",
            $"operational-process-{Uuid.Empty}");

        // Act
        var viaLocation = claim.Claim(Operation(claim, ClaimKind), location, 0, 1, Intent);
        var viaEmptyProcess = claim.Claim(Operation(claim, ClaimKind), emptyProcess, 0, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, viaLocation.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, viaEmptyProcess.Error?.Kind);
        Assert.Empty(Pending(claim));
    }

    [Fact]
    public void ShouldRejectForeignTenantHolderGivenOperationalProcessNameRelease()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = Open(tenantId, "backup process");
        var localHolder = ProcessStream(tenantId);
        Assert.True(claim.Claim(Operation(claim, ClaimKind), localHolder, 0, 1, Intent).IsSuccess);
        var foreignHolder = ProcessStream(Uuid.CreateVersion4());
        var pendingCount = Pending(claim).Length;

        // Act
        var result = claim.Release(Operation(claim, ReleaseKind), foreignHolder, 1, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(pendingCount, Pending(claim).Length);
        Assert.Equal(localHolder, claim.Holder);
    }

    [Fact]
    public void ShouldRejectClaimAndReleaseGivenOperationOfAnotherOwnerOrNoLogicalId()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = Open(tenantId, "backup process");
        var holder = ProcessStream(tenantId);
        Assert.True(claim.Claim(Operation(claim, ClaimKind), holder, 0, 1, Intent).IsSuccess);
        var otherOwner = Open(tenantId, "restore process");
        var pendingCount = Pending(claim).Length;

        // Act
        var foreignClaim = claim.Claim(Operation(otherOwner, ClaimKind), holder, 0, 2, Intent);
        var unnamedClaim = claim.Claim(Operation(claim, ClaimKind, Uuid.Empty), holder, 0, 2, Intent);
        var foreignRelease = claim.Release(Operation(otherOwner, ReleaseKind), holder, 1, 1, Intent);
        var unnamedRelease = claim.Release(Operation(claim, ReleaseKind, Uuid.Empty), holder, 1, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, foreignClaim.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, unnamedClaim.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, foreignRelease.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, unnamedRelease.Error?.Kind);
        Assert.Equal(pendingCount, Pending(claim).Length);
        Assert.Equal(1, claim.HeldGeneration);
    }

    [Fact]
    public void ShouldPinLaterGenerationGivenHolderClaimsAgain()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = Open(tenantId, "backup process");
        var holder = ProcessStream(tenantId);
        Assert.True(claim.Claim(Operation(claim, ClaimKind), holder, 0, 1, Intent).IsSuccess);

        // Act
        var sameGeneration = claim.Claim(Operation(claim, ClaimKind), holder, 0, 1, Intent);
        var laterGeneration = claim.Claim(Operation(claim, ClaimKind), holder, 1, 3, Intent);

        // Assert
        Assert.True(sameGeneration.IsSuccess);
        Assert.True(laterGeneration.IsSuccess);
        Assert.Equal(holder, claim.Holder);
        Assert.Equal(3, claim.HeldGeneration);
        Assert.Equal(3, Pending(claim).Length);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenHolderClaimsEarlierGeneration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = Open(tenantId, "backup process");
        var holder = ProcessStream(tenantId);
        Assert.True(claim.Claim(Operation(claim, ClaimKind), holder, 0, 3, Intent).IsSuccess);

        // Act
        var result = claim.Claim(Operation(claim, ClaimKind), holder, 0, 2, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(Pending(claim));
        Assert.Equal(3, claim.HeldGeneration);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenAnotherProcessHoldsTheName()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = Open(tenantId, "backup process");
        var holder = ProcessStream(tenantId);
        Assert.True(claim.Claim(Operation(claim, ClaimKind), holder, 0, 1, Intent).IsSuccess);

        // Act
        var result = claim.Claim(Operation(claim, ClaimKind), ProcessStream(tenantId), 0, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(Pending(claim));
        Assert.Equal(holder, claim.Holder);
    }

    [Fact]
    public void ShouldFreeNameForAnotherProcessGivenExactHolderAndGenerationRelease()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = Open(tenantId, "backup process");
        var holder = ProcessStream(tenantId);
        var next = ProcessStream(tenantId);
        Assert.True(claim.Claim(Operation(claim, ClaimKind), holder, 0, 2, Intent).IsSuccess);

        // Act
        var release = claim.Release(Operation(claim, ReleaseKind), holder, 1, 2, Intent);
        var takeover = claim.Claim(Operation(claim, ClaimKind), next, 0, 1, Intent);

        // Assert
        Assert.True(release.IsSuccess);
        Assert.True(takeover.IsSuccess);
        Assert.Equal(next, claim.Holder);
        Assert.Equal(1, claim.HeldGeneration);
        Assert.Collection(Pending(claim),
            first => Assert.IsType<OperationalProcessNameClaimed>(first),
            second => Assert.Equal(holder, Assert.IsType<OperationalProcessNameReleased>(second).Owner),
            third => Assert.Equal(next, Assert.IsType<OperationalProcessNameClaimed>(third).Owner));
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenReleaseOfOtherHolderOtherGenerationOrFreeName()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = Open(tenantId, "backup process");
        var holder = ProcessStream(tenantId);
        var freeResult = claim.Release(Operation(claim, ReleaseKind), holder, 0, 1, Intent);
        Assert.True(claim.Claim(Operation(claim, ClaimKind), holder, 0, 2, Intent).IsSuccess);

        // Act
        var otherHolder = claim.Release(Operation(claim, ReleaseKind), ProcessStream(tenantId), 1, 2, Intent);
        var otherGeneration = claim.Release(Operation(claim, ReleaseKind), holder, 1, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, freeResult.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherHolder.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherGeneration.Error?.Kind);
        Assert.Single(Pending(claim));
        Assert.Equal(holder, claim.Holder);
    }

    [Fact]
    public void ShouldKeepOneFactGivenExactClaimRetry()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var logicalId = Uuid.CreateVersion4();
        var claim = Open(tenantId, "backup process", logicalId);
        var holder = ProcessStream(tenantId);
        var operation = Operation(claim, ClaimKind, logicalId);
        Assert.True(claim.Claim(operation, holder, 0, 1, Intent).IsSuccess);

        // Act
        var retry = claim.Claim(operation with { AcceptedAt = Now.AddMinutes(5) }, holder, 0, 1, Intent);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Single(Pending(claim));
        Assert.Equal(holder, claim.Holder);
    }

    [Fact]
    public void ShouldKeepOriginalResultGivenClaimRetryAfterRelease()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claimLogicalId = Uuid.CreateVersion4();
        var source = Open(tenantId, "backup process");
        var holder = ProcessStream(tenantId);
        var claimOperation = Operation(source, ClaimKind, claimLogicalId);
        Assert.True(source.Claim(claimOperation, holder, 0, 1, Intent).IsSuccess);
        Assert.True(source.Release(Operation(source, ReleaseKind), holder, 1, 1, Intent).IsSuccess);
        var claim = NativeFacts.Replay(Open(tenantId, "backup process", claimLogicalId), NativeFacts.Raised(source));
        var pendingCount = Pending(claim).Length;

        // Act
        var retry = claim.Claim(claimOperation, holder, 0, 1, Intent);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(pendingCount, Pending(claim).Length);
        Assert.Null(claim.Holder);
    }

    [Fact]
    public void ShouldKeepOriginalResultGivenReleaseRetryAfterLaterClaim()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var releaseLogicalId = Uuid.CreateVersion4();
        var source = Open(tenantId, "backup process");
        var holder = ProcessStream(tenantId);
        var next = ProcessStream(tenantId);
        var releaseOperation = Operation(source, ReleaseKind, releaseLogicalId);
        Assert.True(source.Claim(Operation(source, ClaimKind), holder, 0, 1, Intent).IsSuccess);
        Assert.True(source.Release(releaseOperation, holder, 1, 1, Intent).IsSuccess);
        Assert.True(source.Claim(Operation(source, ClaimKind), next, 0, 1, Intent).IsSuccess);
        var claim = NativeFacts.Replay(Open(tenantId, "backup process", releaseLogicalId), NativeFacts.Raised(source));
        var pendingCount = Pending(claim).Length;

        // Act
        var retry = claim.Release(releaseOperation, holder, 1, 1, Intent);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(pendingCount, Pending(claim).Length);
        Assert.Equal(next, claim.Holder);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenLogicalIdReusedWithChangedIntent()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var logicalId = Uuid.CreateVersion4();
        var claim = Open(tenantId, "backup process", logicalId);
        var holder = ProcessStream(tenantId);
        var operation = Operation(claim, ClaimKind, logicalId);
        Assert.True(claim.Claim(operation, holder, 0, 1, Intent).IsSuccess);

        // Act
        var changedDigest = claim.Claim(Operation(claim, ClaimKind, logicalId, new string('d', 64)), holder, 0, 1, Intent);
        var changedIntent = claim.Claim(operation, holder, 0, 1, OtherIntent);
        var changedGeneration = claim.Claim(operation, holder, 0, 2, Intent);
        var changedRevision = claim.Claim(operation, holder, 3, 1, Intent);
        var changedHolder = claim.Claim(operation, ProcessStream(tenantId), 0, 1, Intent);
        var changedKind = claim.Release(Operation(claim, ReleaseKind, logicalId), holder, 0, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changedDigest.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedIntent.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedGeneration.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedRevision.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedHolder.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedKind.Error?.Kind);
        Assert.Single(Pending(claim));
        Assert.Equal(holder, claim.Holder);
    }

    [Fact]
    public void ShouldRestoreHolderAndGenerationGivenValidHistoryReplay()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var source = Open(tenantId, "backup process");
        var first = ProcessStream(tenantId);
        var second = ProcessStream(tenantId);
        Assert.True(source.Claim(Operation(source, ClaimKind), first, 0, 1, Intent).IsSuccess);
        Assert.True(source.Release(Operation(source, ReleaseKind), first, 1, 1, Intent).IsSuccess);
        Assert.True(source.Claim(Operation(source, ClaimKind), second, 0, 1, Intent).IsSuccess);
        Assert.True(source.Claim(Operation(source, ClaimKind), second, 1, 4, Intent).IsSuccess);

        // Act
        var replayed = NativeFacts.Replay(Open(tenantId, "backup process"), NativeFacts.Raised(source));

        // Assert
        Assert.Equal(second, replayed.Holder);
        Assert.Equal(4, replayed.HeldGeneration);
        Assert.Empty(Pending(replayed));
    }

    [Fact]
    public void ShouldRefuseCompetingProcessGivenHydratedHistoryHoldsTheName()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var source = Open(tenantId, "backup process");
        var holder = ProcessStream(tenantId);
        Assert.True(source.Claim(Operation(source, ClaimKind), holder, 0, 1, Intent).IsSuccess);
        var history = NativeFacts.Raised(source);
        var first = NativeFacts.Replay(Open(tenantId, "backup process"), history);
        var second = NativeFacts.Replay(Open(tenantId, "backup process"), history);

        // Act
        var competing = second.Claim(Operation(second, ClaimKind), ProcessStream(tenantId), 0, 1, Intent);
        var release = first.Release(Operation(first, ReleaseKind), holder, 1, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, competing.Error?.Kind);
        Assert.Empty(Pending(second));
        Assert.True(release.IsSuccess);
        Assert.Null(first.Holder);
    }

    static OperationalProcessNameClaim Open(Uuid tenantId, string normalizedName, Uuid selectedLogicalId = default) =>
        new(tenantId, OperationalProcessNameClaim.AddressOf(tenantId, normalizedName), selectedLogicalId);

    static EventStreamAddress ProcessStream(Uuid tenantId) =>
        new OperationalProcess(tenantId, Uuid.CreateVersion4()).Stream;

    static DomainEvent[] Pending(OperationalProcessNameClaim claim) =>
        new AggregateScenario<OperationalProcessNameClaim>(claim).PendingEvents.ToArray();

    static OperationProvenance Operation(OperationalProcessNameClaim claim, string commandKind,
        Uuid? logicalId = null, string? intentDigest = null)
    {
        var id = logicalId ?? Uuid.CreateVersion4();
        return new OperationProvenance(id, id, null, commandKind, Actor,
            claim.Stream, "initial", null, intentDigest ?? new string('b', 64), Now, claim.Stream, claim.Stream);
    }
}
