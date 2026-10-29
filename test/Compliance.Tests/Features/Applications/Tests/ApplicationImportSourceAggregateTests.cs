using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

/// <summary>
///     Owner-level tests of one canonical import source: identity, single-holder admission, fences, refusals that
///     append nothing and replay of valid history.
/// </summary>
public sealed class ApplicationImportSourceAggregateTests
{
    static ApplicationImportSource Fresh(Uuid tenantId, Uuid selectedBatchId, string key = "manual",
        string sourceNamespace = "applications") =>
        new(tenantId, key, sourceNamespace, selectedBatchId: selectedBatchId);

    static OperationProvenance Operation(ApplicationImportSource owner, Uuid batchId, string commandKind) =>
        NativeFacts.Root(owner, Uuid.CreateVersion4(), commandKind, null) with
        {
            FirstOwner = new ApplicationImportBatch(owner.TenantId, batchId).Stream,
            CausationEventId = Uuid.CreateVersion4(),
        };

    static Result Claim(ApplicationImportSource owner, Uuid batchId,
        string commandKind = nameof(ClaimApplicationImportSource)) =>
        owner.Claim(Operation(owner, batchId, commandKind), NativeFacts.Ref(), batchId);

    static Result Settle(ApplicationImportSource owner, Uuid batchId, ApplicationImportDisposition disposition,
        string commandKind = nameof(ReleaseApplicationImportSource)) =>
        owner.Settle(Operation(owner, batchId, commandKind), NativeFacts.Ref(), batchId, disposition);

    static int Pending(ApplicationImportSource owner) =>
        new AggregateScenario<ApplicationImportSource>(owner).PendingEvents.Count;

    [Fact]
    public void ShouldKeepStreamOfSourceGivenTenantExactKeyAndNamespaceOnly()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var batchId = Uuid.CreateVersion4();

        // Act
        var first = Fresh(tenantId, batchId);
        var again = Fresh(tenantId, Uuid.CreateVersion4());
        var otherCase = Fresh(tenantId, batchId, "Manual");
        var otherNamespace = Fresh(tenantId, batchId, sourceNamespace: "other");
        var otherTenant = Fresh(Uuid.CreateVersion4(), batchId);

        // Assert
        Assert.Equal(first.Id, again.Id);
        Assert.NotEqual(first.Id, otherCase.Id);
        Assert.NotEqual(first.Id, otherNamespace.Id);
        Assert.NotEqual(first.Id, otherTenant.Id);
        Assert.Equal(ApplicationImportSource.IdFor(tenantId, "manual", "applications"), first.Id);
        Assert.Equal(new EventStreamAddress(tenantId.ToString(), ApplicationImportSource.Area, first.Id.ToString()),
            first.Stream);
    }

    [Fact]
    public void ShouldHoldSourceGivenSelectedBatchClaimOnIdleSource()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var batchId = Uuid.CreateVersion4();
        var owner = Fresh(tenantId, batchId);

        // Act
        var claimed = Claim(owner, batchId);

        // Assert
        Assert.True(claimed.IsSuccess, claimed.Error?.Message);
        var fact = Assert.IsType<ApplicationImportSourceAdmissionRecorded>(
            Assert.Single(new AggregateScenario<ApplicationImportSource>(owner).PendingEvents));
        Assert.Equal(batchId, fact.BatchId);
        Assert.Equal("manual", fact.SourceKey);
        Assert.Equal("applications", fact.SourceNamespace);
        Assert.Equal(1, fact.Revision);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(batchId, owner.ActiveBatchId);
        Assert.True(owner.Find(batchId)!.IsActive);
    }

    [Fact]
    public void ShouldAppendNothingGivenClaimWithInvalidLineageOrUnselectedBatch()
    {
        // Arrange
        var batchId = Uuid.CreateVersion4();
        var owner = Fresh(Uuid.CreateVersion4(), batchId);
        var noFirstOwner = NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(ClaimApplicationImportSource), null) with
        {
            CausationEventId = Uuid.CreateVersion4(),
        };

        // Act
        var wrongCommand = Claim(owner, batchId, nameof(ReleaseApplicationImportSource));
        var withoutFirstOwner = owner.Claim(noFirstOwner, NativeFacts.Ref(), batchId);
        var unselected = Claim(owner, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, wrongCommand.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, withoutFirstOwner.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, unselected.Error?.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.Equal(0, owner.Revision);
        Assert.Null(owner.ActiveBatchId);
    }

    [Fact]
    public void ShouldFenceBatchGivenCanceledSettlementOfNeverAdmittedBatch()
    {
        // Arrange
        var batchId = Uuid.CreateVersion4();
        var owner = Fresh(Uuid.CreateVersion4(), batchId);

        // Act
        var settled = Settle(owner, batchId, ApplicationImportDisposition.Canceled);

        // Assert
        Assert.True(settled.IsSuccess, settled.Error?.Message);
        var fact = Assert.IsType<ApplicationImportSourceSettled>(
            Assert.Single(new AggregateScenario<ApplicationImportSource>(owner).PendingEvents));
        Assert.Equal(ApplicationImportDisposition.Canceled, fact.Disposition);
        Assert.True(owner.Find(batchId)!.IsFence);
        Assert.Null(owner.ActiveBatchId);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenInvalidSettlementOfNeverAdmittedBatch()
    {
        // Arrange
        var batchId = Uuid.CreateVersion4();
        var owner = Fresh(Uuid.CreateVersion4(), batchId);

        // Act
        var published = Settle(owner, batchId, ApplicationImportDisposition.Published);
        var undefined = Settle(owner, batchId, (ApplicationImportDisposition)99);
        var wrongCommand = Settle(owner, batchId, ApplicationImportDisposition.Canceled,
            nameof(ClaimApplicationImportSource));
        var unselected = Settle(owner, Uuid.CreateVersion4(), ApplicationImportDisposition.Canceled);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, published.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, undefined.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, wrongCommand.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, unselected.Error?.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldRestoreHolderGivenValidAdmissionHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var batchId = Uuid.CreateVersion4();
        var seed = Fresh(tenantId, batchId);
        Assert.True(Claim(seed, batchId).IsSuccess);
        var facts = NativeFacts.Raised(seed);

        // Act
        var owner = NativeFacts.Replay(Fresh(tenantId, batchId), facts);

        // Assert
        Assert.Equal(1, owner.Revision);
        Assert.Equal(batchId, owner.ActiveBatchId);
        Assert.IsType<ApplicationImportSourceAdmissionRecorded>(owner.Latest);
        Assert.Equal(0, Pending(owner));
    }

    [Fact]
    public void ShouldRefuseCompetingClaimGivenAnotherBatchHoldsSource()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var holder = Uuid.CreateVersion4();
        var contender = Uuid.CreateVersion4();
        var seed = Fresh(tenantId, holder);
        Assert.True(Claim(seed, holder).IsSuccess);
        var owner = NativeFacts.Replay(Fresh(tenantId, contender), NativeFacts.Raised(seed));

        // Act
        var competing = Claim(owner, contender);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, competing.Error?.Kind);
        Assert.True(competing.Error!.IsTransient);
        Assert.Equal(holder, owner.ActiveBatchId);
        Assert.Null(owner.Find(contender));
        Assert.Equal(0, Pending(owner));
    }

    [Fact]
    public void ShouldRefuseClaimAndSecondSettlementGivenBatchAlreadyFenced()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var batchId = Uuid.CreateVersion4();
        var seed = Fresh(tenantId, batchId);
        Assert.True(Settle(seed, batchId, ApplicationImportDisposition.Canceled).IsSuccess);
        var owner = NativeFacts.Replay(Fresh(tenantId, batchId), NativeFacts.Raised(seed));

        // Act
        var claim = Claim(owner, batchId);
        var again = Settle(owner, batchId, ApplicationImportDisposition.Canceled);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, claim.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, again.Error?.Kind);
        Assert.True(owner.Find(batchId)!.IsFence);
        Assert.Null(owner.ActiveBatchId);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(0, Pending(owner));
    }
}
