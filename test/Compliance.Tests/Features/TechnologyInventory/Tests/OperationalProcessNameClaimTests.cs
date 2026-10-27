using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Processes;
namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;

public sealed class OperationalProcessNameClaimTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    static readonly ImmutableContentRef Intent = new(Uuid.CreateVersion4(), new string('a', 64), 1,
        "application/json", "named-record-change-intent.v1");

    [Fact]
    public void ShouldRejectForeignTenantHolderGivenOperationalProcessNameClaim()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = new OperationalProcessNameClaim(tenantId,
            OperationalProcessNameClaim.AddressOf(tenantId, "backup process"));
        var foreignHolder = new OperationalProcess(Uuid.CreateVersion4(), Uuid.CreateVersion4()).Stream;

        // Act
        var result = claim.Claim(Operation(claim, "ClaimOperationalProcessName"), foreignHolder, 0, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<OperationalProcessNameClaim>(claim).PendingEvents);
    }

    [Fact]
    public void ShouldRejectForeignTenantHolderGivenOperationalProcessNameRelease()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var claim = new OperationalProcessNameClaim(tenantId,
            OperationalProcessNameClaim.AddressOf(tenantId, "backup process"));
        var localHolder = new OperationalProcess(tenantId, Uuid.CreateVersion4()).Stream;
        Assert.True(claim.Claim(Operation(claim, "ClaimOperationalProcessName"), localHolder, 0, 1, Intent).IsSuccess);
        var foreignHolder = new OperationalProcess(Uuid.CreateVersion4(), Uuid.CreateVersion4()).Stream;
        var pendingCount = new AggregateScenario<OperationalProcessNameClaim>(claim).PendingEvents.Count;

        // Act
        var result = claim.Release(Operation(claim, "ReleaseOperationalProcessName"), foreignHolder, 1, 1, Intent);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(pendingCount, new AggregateScenario<OperationalProcessNameClaim>(claim).PendingEvents.Count);
    }

    static OperationProvenance Operation(OperationalProcessNameClaim claim, string commandKind)
    {
        var id = Uuid.CreateVersion4();
        var actor = ActorReference.ForSystemProcess("name-claim-test", "Name claim test");
        return new OperationProvenance(id, id, null, commandKind, actor,
            claim.Stream, "initial", null, new string('b', 64), Now, claim.Stream, claim.Stream);
    }
}
