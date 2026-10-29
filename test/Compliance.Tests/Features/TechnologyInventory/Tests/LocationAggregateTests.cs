using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;

public sealed class LocationAggregateTests
{
    [Fact]
    public void ShouldRejectRevisionFromAnotherTenantGivenLocationReplay()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var locationId = Uuid.CreateVersion4();
        var source = NamedRecordFacts.RecordedLocation(tenantId, locationId);
        var history = new AggregateScenario<Location>(source).PendingEvents.ToArray();
        var revision = Assert.IsType<LocationRevisionRecorded>(history[1]);
        history[1] = revision with { TenantId = Uuid.CreateVersion4() };

        // Act
        var error = Record.Exception(() => NativeFacts.Replay(new Location(tenantId, locationId), history));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public void ShouldHoldPendingChangeGivenAcceptedCreation()
    {
        // Arrange
        var owner = new Location(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();
        var intent = NamedRecordFacts.Ref(RecordNameChangeIntent.Schema);

        // Act
        var result = owner.AcceptChange(Operation(owner, root, root, "RecordLocation", null), true, 0, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(0, owner.Revision);
        Assert.Equal(1, owner.Generation);
        Assert.True(owner.IsPending(root, 1));
        Assert.IsType<LocationChangeAccepted>(Assert.Single(Facts(owner)));
    }

    [Fact]
    public void ShouldRefuseSecondChangeAndAppendNothingGivenPendingChange()
    {
        // Arrange
        var owner = new Location(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();
        Assert.True(owner.AcceptChange(Operation(owner, root, root, "RecordLocation", null), true, 0,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"))
            .IsSuccess);
        var other = Uuid.CreateVersion4();

        // Act
        var result = owner.AcceptChange(Operation(owner, other, other, "ReviseLocation", 0), false, 0,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(Facts(owner));
        Assert.True(owner.IsPending(root, 1));
    }

    [Fact]
    public void ShouldRefuseRevisionAndAppendNothingGivenUnrecordedLocation()
    {
        // Arrange
        var owner = new Location(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();

        // Act
        var result = owner.AcceptChange(Operation(owner, root, root, "ReviseLocation", 0), false, 0,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldRefuseCreationAndAppendNothingGivenExistingLocation()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedLocation(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var before = Facts(owner).Length;
        var root = Uuid.CreateVersion4();

        // Act
        var result = owner.AcceptChange(Operation(owner, root, root, "RecordLocation", null), true, 0,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before, Facts(owner).Length);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseStaleRevisionAndAppendNothingGivenAdvancedLocation()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedLocation(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2);
        var before = Facts(owner).Length;
        var root = Uuid.CreateVersion4();

        // Act
        var result = owner.AcceptChange(Operation(owner, root, root, "ReviseLocation", 1), false, 1,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(before, Facts(owner).Length);
        Assert.Equal(2, owner.Revision);
        Assert.Null(owner.Pending);
    }

    [Fact]
    public void ShouldRefuseOperationOfAnotherOwnerAndAppendNothingGivenForeignStream()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var owner = new Location(tenantId, Uuid.CreateVersion4());
        var foreign = new Location(tenantId, Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();

        // Act
        var result = owner.AcceptChange(Operation(foreign, root, root, "RecordLocation", null), true, 0,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldRefuseApplyAndAppendNothingGivenNoPendingChange()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedLocation(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var before = Facts(owner).Length;
        var root = Uuid.CreateVersion4();

        // Act
        var result = owner.ApplyChange(Operation(owner, Uuid.CreateVersion4(), root, "ApplyLocationChange", null),
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref(LocationRevisionRecorded.ContentSchema),
            NamedRecordFacts.Author, NamedRecordFacts.At, NamedRecordFacts.Ref("mutation-result-content.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before, Facts(owner).Length);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseApplyAndKeepPendingGivenDifferentIntent()
    {
        // Arrange
        var owner = new Location(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();
        Assert.True(owner.AcceptChange(Operation(owner, root, root, "RecordLocation", null), true, 0,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"))
            .IsSuccess);

        // Act
        var result = owner.ApplyChange(Operation(owner, Uuid.CreateVersion4(), root, "ApplyLocationChange", null),
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref(LocationRevisionRecorded.ContentSchema),
            NamedRecordFacts.Author, NamedRecordFacts.At, NamedRecordFacts.Ref("mutation-result-content.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(Facts(owner));
        Assert.True(owner.IsPending(root, 1));
    }

    [Fact]
    public void ShouldFenceLateApplyAndAllowNextGenerationGivenCanceledRevision()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedLocation(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();
        var intent = NamedRecordFacts.Ref(RecordNameChangeIntent.Schema);
        Assert.True(owner.AcceptChange(Operation(owner, root, root, "ReviseLocation", 1), false, 1, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1")).IsSuccess);
        Assert.True(owner.CancelChange(Operation(owner, Uuid.CreateVersion4(), root, "CancelLocationChange", null),
            NamedRecordFacts.Ref("failure-proof.v1")).IsSuccess);
        var next = Uuid.CreateVersion4();

        // Act
        var late = owner.ApplyChange(Operation(owner, Uuid.CreateVersion4(), root, "ApplyLocationChange", null), intent,
            NamedRecordFacts.Ref(LocationRevisionRecorded.ContentSchema), NamedRecordFacts.Author, NamedRecordFacts.At,
            NamedRecordFacts.Ref("mutation-result-content.v1"));
        var retried = owner.AcceptChange(Operation(owner, next, next, "ReviseLocation", 1), false, 1, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, late.Error?.Kind);
        Assert.True(retried.IsSuccess, retried.Error?.Message);
        Assert.Equal(3, owner.Generation);
        Assert.Equal(1, owner.Revision);
        Assert.True(owner.IsPending(next, 3));
        Assert.Collection(Facts(owner), fact => Assert.IsType<LocationChangeAccepted>(fact),
            fact => Assert.IsType<LocationRevisionRecorded>(fact), fact => Assert.IsType<LocationChangeAccepted>(fact),
            fact => Assert.IsType<LocationChangeCanceled>(fact), fact => Assert.IsType<LocationChangeAccepted>(fact));
    }

    [Fact]
    public void ShouldRefuseCreationAndAppendNothingGivenCanceledCreation()
    {
        // Arrange
        var owner = new Location(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();
        var intent = NamedRecordFacts.Ref(RecordNameChangeIntent.Schema);
        Assert.True(owner.AcceptChange(Operation(owner, root, root, "RecordLocation", null), true, 0, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1")).IsSuccess);
        Assert.True(owner.CancelChange(Operation(owner, Uuid.CreateVersion4(), root, "CancelLocationChange", null),
            NamedRecordFacts.Ref("failure-proof.v1")).IsSuccess);
        var next = Uuid.CreateVersion4();

        // Act
        var creation = owner.AcceptChange(Operation(owner, next, next, "RecordLocation", null), true, 0, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1"));
        var revision = owner.AcceptChange(Operation(owner, next, next, "ReviseLocation", 0), false, 0, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, creation.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, revision.Error?.Kind);
        Assert.Equal(2, Facts(owner).Length);
        Assert.Null(owner.Pending);
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseCancelAndAppendNothingGivenAppliedChange()
    {
        // Arrange
        var owner = new Location(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();
        var intent = NamedRecordFacts.Ref(RecordNameChangeIntent.Schema);
        Assert.True(owner.AcceptChange(Operation(owner, root, root, "RecordLocation", null), true, 0, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1")).IsSuccess);
        Assert.True(owner.ApplyChange(Operation(owner, Uuid.CreateVersion4(), root, "ApplyLocationChange", null), intent,
            NamedRecordFacts.Ref(LocationRevisionRecorded.ContentSchema), NamedRecordFacts.Author, NamedRecordFacts.At,
            NamedRecordFacts.Ref("mutation-result-content.v1")).IsSuccess);

        // Act
        var result = owner.CancelChange(Operation(owner, Uuid.CreateVersion4(), root, "CancelLocationChange", null),
            NamedRecordFacts.Ref("failure-proof.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(2, Facts(owner).Length);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldKeepOnlyLastTwoRevisionsGivenThreeAppliedRevisions()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedLocation(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 3);

        // Act
        var current = owner.At(3);
        var previous = owner.At(2);
        var dropped = owner.At(1);

        // Assert
        Assert.NotNull(current);
        Assert.NotNull(previous);
        Assert.Null(dropped);
        Assert.Equal(3, owner.Revision);
    }

    [Fact]
    public void ShouldRestoreRevisionAndPendingChangeGivenValidHistoryReplay()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var locationId = Uuid.CreateVersion4();
        var source = NamedRecordFacts.RecordedLocation(tenantId, locationId, 2);
        var root = Uuid.CreateVersion4();
        Assert.True(source.AcceptChange(Operation(source, root, root, "ReviseLocation", 2), false, 2,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"))
            .IsSuccess);
        var history = NativeFacts.Raised(source);

        // Act
        var replayed = NativeFacts.Replay(new Location(tenantId, locationId), history);

        // Assert
        Assert.Equal(2, replayed.Revision);
        Assert.Equal(3, replayed.Generation);
        Assert.True(replayed.IsPending(root, 3));
        Assert.Equal(2, replayed.Current?.Revision);
        Assert.NotNull(replayed.At(1));
    }

    [Fact]
    public void ShouldResolveOriginalAcceptanceGivenReplayAfterLaterFacts()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var locationId = Uuid.CreateVersion4();
        var source = new Location(tenantId, locationId);
        var root = Uuid.CreateVersion4();
        var intent = NamedRecordFacts.Ref(RecordNameChangeIntent.Schema);
        Assert.True(source.AcceptChange(Operation(source, root, root, "RecordLocation", null), true, 0, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1")).IsSuccess);
        Assert.True(source.ApplyChange(Operation(source, Uuid.CreateVersion4(), root, "ApplyLocationChange", null),
            intent, NamedRecordFacts.Ref(LocationRevisionRecorded.ContentSchema), NamedRecordFacts.Author,
            NamedRecordFacts.At, NamedRecordFacts.Ref("mutation-result-content.v1")).IsSuccess);
        var later = Uuid.CreateVersion4();
        Assert.True(source.AcceptChange(Operation(source, later, later, "ReviseLocation", 1), false, 1, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1")).IsSuccess);
        var history = NativeFacts.Raised(source);

        // Act
        var replayed = NativeFacts.Replay(new Location(tenantId, locationId, root), history);

        // Assert
        Assert.IsType<LocationChangeAccepted>(replayed.Acceptance.SelectedFact);
        Assert.Equal("RecordLocation", replayed.Acceptance.SelectedProvenance?.CommandKind);
        Assert.True(replayed.IsPending(later, 2));
    }

    static DomainEvent[] Facts(Location owner) => new AggregateScenario<Location>(owner).PendingEvents.ToArray();

    static OperationProvenance Operation(Location owner, Uuid logicalId, Uuid rootId, string commandKind,
        ulong? expected) =>
        new(logicalId, rootId, null, commandKind, NamedRecordFacts.Author, owner.Stream, "current", expected,
            new string('a', 64), NamedRecordFacts.At, owner.Stream, owner.Stream);
}
