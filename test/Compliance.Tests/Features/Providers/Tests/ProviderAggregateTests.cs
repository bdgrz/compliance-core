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
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.NativeOperations.Services;
namespace Bdgrz.Compliance.Tests.Features.Providers.Tests;

/// <summary>
///     The provider record owner on its own: one tenant-scoped stream, one pending change at a time, a generation
///     fence for delayed applies, revisions that advance only by applied changes, and exact replay. Name uniqueness
///     is decided by <c>ProviderNameClaim</c>, not here.
/// </summary>
public sealed class ProviderAggregateTests
{
    [Fact]
    public void ShouldHoldPendingChangeGivenAcceptedCreation()
    {
        // Arrange
        var owner = new Provider(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();

        // Act
        var result = owner.AcceptChange(Operation(owner, root, root, "RecordProvider", null), true, 0,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(0, owner.Revision);
        Assert.Equal(1, owner.Generation);
        Assert.True(owner.IsPending(root, 1));
        Assert.IsType<ProviderChangeAccepted>(Assert.Single(Facts(owner)));
    }

    [Fact]
    public void ShouldRaiseRecordedThenRevisedGivenTwoAppliedChanges()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var owner = NamedRecordFacts.RecordedProvider(tenantId, providerId);

        // Act
        owner.Revise();
        var facts = Facts(owner);

        // Assert
        var recorded = Assert.IsType<ProviderRecorded>(facts[1]);
        var revised = Assert.IsType<ProviderRevised>(facts[3]);
        Assert.Equal((tenantId, providerId), (recorded.TenantId, recorded.ProviderId));
        Assert.Equal((tenantId, providerId, 2L), (revised.TenantId, revised.ProviderId, revised.Revision));
        Assert.Equal(2, owner.Revision);
        Assert.Null(owner.Pending);
    }

    [Fact]
    public void ShouldRefuseSecondChangeAndAppendNothingGivenPendingChange()
    {
        // Arrange
        var owner = new Provider(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();
        Assert.True(owner.AcceptChange(Operation(owner, root, root, "RecordProvider", null), true, 0,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"))
            .IsSuccess);
        var other = Uuid.CreateVersion4();

        // Act
        var result = owner.AcceptChange(Operation(owner, other, other, "ReviseProvider", 0), false, 0,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(Facts(owner));
        Assert.True(owner.IsPending(root, 1));
    }

    [Fact]
    public void ShouldRefuseRevisionAndAppendNothingGivenUnrecordedProvider()
    {
        // Arrange
        var owner = new Provider(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();

        // Act
        var result = owner.AcceptChange(Operation(owner, root, root, "ReviseProvider", 0), false, 0,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldRefuseCreationAndAppendNothingGivenExistingProvider()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedProvider(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var before = Facts(owner).Length;
        var root = Uuid.CreateVersion4();

        // Act
        var result = owner.AcceptChange(Operation(owner, root, root, "RecordProvider", null), true, 0,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before, Facts(owner).Length);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseStaleRevisionAndAppendNothingGivenAdvancedProvider()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedProvider(Uuid.CreateVersion4(), Uuid.CreateVersion4()).Revise();
        var before = Facts(owner).Length;
        var root = Uuid.CreateVersion4();

        // Act
        var result = owner.AcceptChange(Operation(owner, root, root, "ReviseProvider", 1), false, 1,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before, Facts(owner).Length);
        Assert.Equal(2, owner.Revision);
        Assert.Null(owner.Pending);
    }

    [Fact]
    public void ShouldRefuseOperationOfAnotherOwnerAndAppendNothingGivenForeignStream()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var owner = new Provider(tenantId, Uuid.CreateVersion4());
        var foreign = new Provider(tenantId, Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();

        // Act
        var result = owner.AcceptChange(Operation(foreign, root, root, "RecordProvider", null), true, 0,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(Facts(owner));
    }

    [Fact]
    public void ShouldRefuseApplyAndAppendNothingGivenNoPendingChange()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedProvider(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var before = Facts(owner).Length;
        var root = Uuid.CreateVersion4();

        // Act
        var result = owner.ApplyChange(Operation(owner, Uuid.CreateVersion4(), root, "ApplyProviderChange", null),
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref(ProviderRevised.ContentSchema),
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
        var owner = NamedRecordFacts.RecordedProvider(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();
        Assert.True(owner.AcceptChange(Operation(owner, root, root, "ReviseProvider", 1), false, 1,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"))
            .IsSuccess);
        var before = Facts(owner).Length;

        // Act
        var result = owner.ApplyChange(Operation(owner, Uuid.CreateVersion4(), root, "ApplyProviderChange", null),
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref(ProviderRevised.ContentSchema),
            NamedRecordFacts.Author, NamedRecordFacts.At, NamedRecordFacts.Ref("mutation-result-content.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before, Facts(owner).Length);
        Assert.Equal(1, owner.Revision);
        Assert.True(owner.IsPending(root, 2));
    }

    [Fact]
    public void ShouldRefuseCancelAndKeepPendingGivenDifferentRoot()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedProvider(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();
        Assert.True(owner.AcceptChange(Operation(owner, root, root, "ReviseProvider", 1), false, 1,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"))
            .IsSuccess);
        var before = Facts(owner).Length;

        // Act
        var result = owner.CancelChange(Operation(owner, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "CancelProviderChange", null), NamedRecordFacts.Ref("failure-proof.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before, Facts(owner).Length);
        Assert.True(owner.IsPending(root, 2));
    }

    [Fact]
    public void ShouldFenceLateApplyAndAllowNextGenerationGivenCanceledRevision()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedProvider(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();
        var intent = NamedRecordFacts.Ref(RecordNameChangeIntent.Schema);
        Assert.True(owner.AcceptChange(Operation(owner, root, root, "ReviseProvider", 1), false, 1, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1")).IsSuccess);
        Assert.True(owner.CancelChange(Operation(owner, Uuid.CreateVersion4(), root, "CancelProviderChange", null),
            NamedRecordFacts.Ref("failure-proof.v1")).IsSuccess);
        var next = Uuid.CreateVersion4();

        // Act
        var late = owner.ApplyChange(Operation(owner, Uuid.CreateVersion4(), root, "ApplyProviderChange", null), intent,
            NamedRecordFacts.Ref(ProviderRevised.ContentSchema), NamedRecordFacts.Author, NamedRecordFacts.At,
            NamedRecordFacts.Ref("mutation-result-content.v1"));
        var retried = owner.AcceptChange(Operation(owner, next, next, "ReviseProvider", 1), false, 1, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, late.Error?.Kind);
        Assert.True(retried.IsSuccess, retried.Error?.Message);
        Assert.Equal(3, owner.Generation);
        Assert.Equal(1, owner.Revision);
        Assert.True(owner.IsPending(next, 3));
        Assert.Collection(Facts(owner), fact => Assert.IsType<ProviderChangeAccepted>(fact),
            fact => Assert.IsType<ProviderRecorded>(fact), fact => Assert.IsType<ProviderChangeAccepted>(fact),
            fact => Assert.IsType<ProviderChangeCanceled>(fact), fact => Assert.IsType<ProviderChangeAccepted>(fact));
    }

    [Fact]
    public void ShouldRefuseCreationAndAppendNothingGivenCanceledCreation()
    {
        // Arrange
        var owner = new Provider(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();
        var intent = NamedRecordFacts.Ref(RecordNameChangeIntent.Schema);
        Assert.True(owner.AcceptChange(Operation(owner, root, root, "RecordProvider", null), true, 0, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1")).IsSuccess);
        Assert.True(owner.CancelChange(Operation(owner, Uuid.CreateVersion4(), root, "CancelProviderChange", null),
            NamedRecordFacts.Ref("failure-proof.v1")).IsSuccess);
        var next = Uuid.CreateVersion4();

        // Act
        var revision = owner.AcceptChange(Operation(owner, next, next, "ReviseProvider", 0), false, 0, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, revision.Error?.Kind);
        Assert.Equal(2, Facts(owner).Length);
        Assert.Null(owner.Pending);
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseRepeatedCreationAndAppendNothingGivenCanceledCreation()
    {
        // Arrange
        var owner = new Provider(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var root = Uuid.CreateVersion4();
        var intent = NamedRecordFacts.Ref(RecordNameChangeIntent.Schema);
        Assert.True(owner.AcceptChange(Operation(owner, root, root, "RecordProvider", null), true, 0, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1")).IsSuccess);
        Assert.True(owner.CancelChange(Operation(owner, Uuid.CreateVersion4(), root, "CancelProviderChange", null),
            NamedRecordFacts.Ref("failure-proof.v1")).IsSuccess);
        var next = Uuid.CreateVersion4();

        // Act
        var creation = owner.AcceptChange(Operation(owner, next, next, "RecordProvider", null), true, 0, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, creation.Error?.Kind);
        Assert.Equal(2, Facts(owner).Length);
        Assert.Null(owner.Pending);
    }

    [Fact]
    public void ShouldRefuseCancelAndAppendNothingGivenAppliedChange()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedProvider(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var before = Facts(owner).Length;
        var root = Uuid.CreateVersion4();

        // Act
        var result = owner.CancelChange(Operation(owner, Uuid.CreateVersion4(), root, "CancelProviderChange", null),
            NamedRecordFacts.Ref("failure-proof.v1"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before, Facts(owner).Length);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldKeepOnlyLastTwoRevisionsGivenThreeAppliedRevisions()
    {
        // Arrange
        var owner = NamedRecordFacts.RecordedProvider(Uuid.CreateVersion4(), Uuid.CreateVersion4()).Revise().Revise();

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
        var providerId = Uuid.CreateVersion4();
        var source = NamedRecordFacts.RecordedProvider(tenantId, providerId).Revise();
        var root = Uuid.CreateVersion4();
        Assert.True(source.AcceptChange(Operation(source, root, root, "ReviseProvider", 2), false, 2,
            NamedRecordFacts.Ref(RecordNameChangeIntent.Schema), NamedRecordFacts.Ref("first-response-descriptor.v1"))
            .IsSuccess);
        var history = NativeFacts.Raised(source);

        // Act
        var replayed = NativeFacts.Replay(new Provider(tenantId, providerId), history);

        // Assert
        Assert.Equal(2, replayed.Revision);
        Assert.Equal(3, replayed.Generation);
        Assert.True(replayed.IsPending(root, 3));
        Assert.Equal(2, replayed.Current?.Revision);
        Assert.NotNull(replayed.At(1));
    }

    [Fact]
    public void ShouldReturnOriginalAcceptanceGivenExactRetryAfterLaterFactsAndConflictGivenChangedIntent()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var source = new Provider(tenantId, providerId);
        var root = Uuid.CreateVersion4();
        var intent = NamedRecordFacts.Ref(RecordNameChangeIntent.Schema);
        Assert.True(source.AcceptChange(Operation(source, root, root, "RecordProvider", null), true, 0, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1")).IsSuccess);
        Assert.True(source.ApplyChange(Operation(source, Uuid.CreateVersion4(), root, "ApplyProviderChange", null),
            intent, NamedRecordFacts.Ref(ProviderRecorded.ContentSchema), NamedRecordFacts.Author,
            NamedRecordFacts.At, NamedRecordFacts.Ref("mutation-result-content.v1")).IsSuccess);
        var later = Uuid.CreateVersion4();
        Assert.True(source.AcceptChange(Operation(source, later, later, "ReviseProvider", 1), false, 1, intent,
            NamedRecordFacts.Ref("first-response-descriptor.v1")).IsSuccess);
        var history = NativeFacts.Raised(source);
        var accepted = Assert.IsType<ProviderChangeAccepted>(history[0]);

        // Act
        var replayed = NativeFacts.Replay(new Provider(tenantId, providerId, root), history);
        var exact = replayed.Acceptance.Decide(AcceptedOperationBinding.Of(accepted.Operation));
        var changed = replayed.Acceptance.Decide(AcceptedOperationBinding.Of(
            accepted.Operation with { AuthoredIntentSha256 = new string('c', 64) }));

        // Assert
        Assert.IsType<ProviderChangeAccepted>(replayed.Acceptance.SelectedFact);
        Assert.Equal("RecordProvider", replayed.Acceptance.SelectedProvenance?.CommandKind);
        Assert.True(replayed.IsPending(later, 2));
        Assert.Equal(NativeAcceptanceDecision.Replayed, exact);
        Assert.Equal(NativeAcceptanceDecision.Conflict, changed);
    }

    static DomainEvent[] Facts(Provider owner) => new AggregateScenario<Provider>(owner).PendingEvents.ToArray();

    static OperationProvenance Operation(Provider owner, Uuid logicalId, Uuid rootId, string commandKind,
        ulong? expected) =>
        new(logicalId, rootId, null, commandKind, NamedRecordFacts.Author, owner.Stream, "current", expected,
            new string('a', 64), NamedRecordFacts.At, owner.Stream, owner.Stream);
}
