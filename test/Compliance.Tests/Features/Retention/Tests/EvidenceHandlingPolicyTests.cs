using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Retention.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
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
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Retention.Tests;

public sealed class EvidenceHandlingPolicyTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ArtifactId = Uuid.CreateVersion4();
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Policy owner");
    static readonly DateTimeOffset Now = new(2026, 10, 17, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldBindReviewedClassToExactArtifactIdentityGivenAssignment()
    {
        // Arrange
        var policy = NewPolicy();

        // Act
        var assigned = policy.Assign(Source(), 0, "restricted", 1,
            "Contains regulated customer records", Actor, Now);

        // Assert
        Assert.True(assigned.IsSuccess);
        Assert.Equal("restricted", policy.Current!.HandlingClass);
        Assert.Equal("internal", policy.Current.SourceHandlingClass);
        Assert.Equal(Source().Source.ContentSha256, policy.Current.Source.ContentSha256);
        Assert.Equal(1, policy.Revision);
        var ev = Assert.IsType<ArtifactHandlingPolicyAssigned>(
            Assert.Single(new AggregateScenario<EvidenceHandlingPolicy>(policy).PendingEvents));
        Assert.Equal(policy.Current.HandlingClass, ev.State.HandlingClass);
        Assert.Equal(policy.Current.Source.ContentSha256, ev.State.Source.ContentSha256);
        Assert.Equal(policy.Current.History.Count, ev.State.History.Count);
    }

    [Fact]
    public void ShouldRejectChangedSourceOrUnsupportedClassGivenAssignment()
    {
        // Arrange
        var policy = NewPolicy();
        Assert.True(policy.Assign(Source(), 0, "confidential", 1, "Reviewed", Actor, Now).IsSuccess);

        // Act
        var changedContent = policy.Assign(Source() with
        {
            Source = Source().Source with { ContentSha256 = new string('b', 64) }
        }, 1, "restricted", 1, "Reclassified", Actor, Now.AddDays(1));
        var unsupportedClass = policy.Assign(Source(), 1, "secret", 1,
            "Unsupported class", Actor, Now.AddDays(1));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changedContent.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(unsupportedClass.Error).Kind);
        Assert.Equal("confidential", policy.Current!.HandlingClass);
        Assert.Equal(1, policy.Revision);
    }

    [Fact]
    public void ShouldRequireCurrentRevisionAndReasonForReclassificationGivenAssignment()
    {
        // Arrange
        var policy = NewPolicy();
        Assert.True(policy.Assign(Source(), 0, "internal", 1, "Initial review", Actor, Now).IsSuccess);

        // Act
        var stale = policy.Assign(Source(), 0, "confidential", 1, "Changed handling", Actor,
            Now.AddDays(1));
        var missingReason = policy.Assign(Source(), 1, "confidential", 1, " ", Actor,
            Now.AddDays(1));
        var revised = policy.Assign(Source(), 1, "confidential", 1, "Customer data identified",
            Actor, Now.AddDays(1));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(stale.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(missingReason.Error).Kind);
        Assert.True(revised.IsSuccess);
        Assert.Equal("confidential", policy.Current!.HandlingClass);
        Assert.Equal(2, policy.Revision);
        Assert.Equal(2, policy.Current.History.Count);
        Assert.Equal("internal", policy.Current.History[0].HandlingClass);
        Assert.Equal("Customer data identified", policy.Current.History[1].Rationale);
    }

    [Fact]
    public void ShouldRejectSecretRejectedArtifactGivenAssignment()
    {
        // Arrange
        var policy = NewPolicy();

        // Act
        var rejected = policy.Assign(Source() with { SourceState = "rejected" }, 0,
            "restricted", 1, "Secret-bearing content was rejected", Actor, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(rejected.Error).Kind);
        Assert.Null(policy.Current);
        Assert.Empty(new AggregateScenario<EvidenceHandlingPolicy>(policy).PendingEvents);
    }

    [Fact]
    public void ShouldAllowOnlyExactRetryAndReplayClassificationHistoryGivenReclassification()
    {
        // Arrange
        var policy = NewPolicy();
        Assert.True(policy.Assign(Source(), 0, "internal", 1, "Initial review", Actor, Now).IsSuccess);
        var events = new AggregateScenario<EvidenceHandlingPolicy>(policy).PendingEvents.ToArray();

        // Act
        var exactRetry = policy.Assign(Source(), 0, "internal", 1, "Initial review", Actor, Now);
        var changedRetry = policy.Assign(Source(), 0, "confidential", 1, "Changed handling", Actor, Now);
        Assert.True(policy.Assign(Source(), 1, "confidential", 1, "New data category", Actor,
            Now.AddDays(1)).IsSuccess);
        var replay = new AggregateScenario<EvidenceHandlingPolicy>(NewPolicy()).Given(
            [.. events, .. new AggregateScenario<EvidenceHandlingPolicy>(policy).PendingEvents.Skip(1)])
            .Aggregate;

        // Assert
        Assert.True(exactRetry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changedRetry.Error).Kind);
        Assert.Equal(policy.Revision, replay.Revision);
        Assert.Equal(policy.Current!.HandlingClass, replay.Current!.HandlingClass);
        Assert.Equal(policy.Current.History, replay.Current.History);
    }

    [Fact]
    public void ShouldReturnOriginalResultGivenRetryOfCommittedDecisionAfterLaterDecision()
    {
        // Arrange
        var policy = NewPolicy();
        Assert.True(policy.Assign(Source(), 0, "internal", 1, "Initial review", Actor, Now).IsSuccess);
        Assert.True(policy.Assign(Source(), 1, "confidential", 1, "New data category", Actor,
            Now.AddDays(1)).IsSuccess);

        // Act
        var retry = policy.Assign(Source(), 0, "internal", 1, "  Initial review ", Actor, Now.AddDays(2));
        var changedRetry = policy.Assign(Source(), 0, "internal", 1, "Different reason", Actor, Now.AddDays(2));

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changedRetry.Error).Kind);
        Assert.Equal(2, policy.Revision);
        Assert.Equal(2, new AggregateScenario<EvidenceHandlingPolicy>(policy).PendingEvents.Count);
    }

    [Fact]
    public void ShouldTreatRetryWithFreshTimestampAsNoOpGivenSameIntent()
    {
        // Arrange
        var policy = NewPolicy();
        Assert.True(policy.Assign(Source(), 0, "internal", 1, "Initial review", Actor, Now).IsSuccess);

        // Act
        var retry = policy.Assign(Source(), 0, "internal", 1, "Initial review", Actor, Now.AddMinutes(5));
        var changedActor = policy.Assign(Source(), 0, "internal", 1, "Initial review",
            ActorReference.ForMember(Uuid.CreateVersion4(), "Other owner"), Now.AddMinutes(5));

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changedActor.Error).Kind);
        Assert.Equal(Now, policy.Current!.AssignedAt);
        Assert.Single(new AggregateScenario<EvidenceHandlingPolicy>(policy).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseChangedRationaleGivenSameClassAtCurrentRevision()
    {
        // Arrange
        var policy = NewPolicy();
        Assert.True(policy.Assign(Source(), 0, "internal", 1, "Initial review", Actor, Now).IsSuccess);

        // Act
        var changed = policy.Assign(Source(), 1, "internal", 1, "Another reason", Actor, Now.AddDays(1));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.Equal("Initial review", policy.Current!.Rationale);
        Assert.Single(new AggregateScenario<EvidenceHandlingPolicy>(policy).PendingEvents);
    }

    [Fact]
    public void ShouldAcknowledgeWithoutNewFactGivenSameDecisionAtCurrentRevision()
    {
        // Arrange
        var policy = NewPolicy();
        Assert.True(policy.Assign(Source(), 0, "internal", 1, "Initial review", Actor, Now).IsSuccess);

        // Act
        var same = policy.Assign(Source(), 1, "internal", 1, "Initial review", Actor, Now.AddDays(1));

        // Assert
        Assert.True(same.IsSuccess);
        Assert.Equal(1, policy.Revision);
        Assert.Equal(Now, policy.Current!.AssignedAt);
        Assert.Single(new AggregateScenario<EvidenceHandlingPolicy>(policy).PendingEvents);
    }

    [Fact]
    public void ShouldAppendNothingGivenInvalidAssignment()
    {
        // Arrange
        var policy = NewPolicy();
        var foreignSource = Source() with
        {
            Source = Source().Source with { TenantId = Uuid.CreateVersion4() }
        };
        var systemActor = ActorReference.ForSystemProcess("importer", "Importer");

        // Act
        var foreign = policy.Assign(foreignSource, 0, "internal", 1, "Reviewed", Actor, Now);
        var wrongKind = policy.Assign(Source() with
        {
            Source = Source().Source with { SourceKind = "policy_document" }
        }, 0, "internal", 1, "Reviewed", Actor, Now);
        var nonMember = policy.Assign(Source(), 0, "internal", 1, "Reviewed", systemActor, Now);
        var unsupportedVersion = policy.Assign(Source(), 0, "internal", 2, "Reviewed", Actor, Now);
        var noTime = policy.Assign(Source(), 0, "internal", 1, "Reviewed", Actor, default);
        var tooLong = policy.Assign(Source(), 0, "internal", 1, new string('x', 2001), Actor, Now);
        var staleFirst = policy.Assign(Source(), 3, "internal", 1, "Reviewed", Actor, Now);
        var badDigest = policy.Assign(Source() with
        {
            Source = Source().Source with { ContentSha256 = "ABC" }
        }, 0, "internal", 1, "Reviewed", Actor, Now);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(foreign.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(wrongKind.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(nonMember.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(unsupportedVersion.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(noTime.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(tooLong.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(staleFirst.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(badDigest.Error).Kind);
        Assert.Null(policy.Current);
        Assert.Empty(new AggregateScenario<EvidenceHandlingPolicy>(policy).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseRegressedSourcePositionGivenLaterAssignment()
    {
        // Arrange
        var policy = NewPolicy();
        Assert.True(policy.Assign(Source(), 0, "internal", 1, "Initial review", Actor, Now).IsSuccess);

        // Act
        var regressed = policy.Assign(Source() with { Position = 5 }, 1, "restricted", 1,
            "Reclassified", Actor, Now.AddDays(1));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(regressed.Error).Kind);
        Assert.Equal("internal", policy.Current!.HandlingClass);
        Assert.Single(new AggregateScenario<EvidenceHandlingPolicy>(policy).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseNegativeRevisionGivenExistingPolicy()
    {
        // Arrange
        var policy = NewPolicy();
        Assert.True(policy.Assign(Source(), 0, "internal", 1, "Initial review", Actor, Now).IsSuccess);

        // Act
        var negative = policy.Assign(Source(), -1, "internal", 1, "Initial review", Actor, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(negative.Error).Kind);
        Assert.Single(new AggregateScenario<EvidenceHandlingPolicy>(policy).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseFurtherRevisionGivenRetainedVersionLimit()
    {
        // Arrange
        var policy = NewPolicy();
        var classes = new[] { "public", "internal", "confidential", "restricted" };
        for (var revision = 0; revision < 32; revision++)
            Assert.True(policy.Assign(Source(), revision, classes[revision % 4], 1,
                $"Review {revision}", Actor, Now.AddDays(revision)).IsSuccess);

        // Act
        var overflow = policy.Assign(Source(), 32, classes[32 % 4], 1, "One too many", Actor, Now.AddDays(40));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(overflow.Error).Kind);
        Assert.Equal(32, policy.Revision);
        Assert.Equal(32, new AggregateScenario<EvidenceHandlingPolicy>(policy).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRestoreStateAndAcceptRetryAndNextDecisionGivenValidHistoryReplay()
    {
        // Arrange
        var seed = NewPolicy();
        Assert.True(seed.Assign(Source(), 0, "internal", 1, "Initial review", Actor, Now).IsSuccess);
        Assert.True(seed.Assign(Source(), 1, "confidential", 1, "New data category", Actor,
            Now.AddDays(1)).IsSuccess);
        var history = NativeFacts.Raised(seed);

        // Act
        var replayed = NativeFacts.Replay(NewPolicy(), history);
        var restoredClass = replayed.Current!.HandlingClass;
        var restoredRevision = replayed.Revision;
        var retry = replayed.Assign(Source(), 0, "internal", 1, "Initial review", Actor, Now.AddDays(3));
        var next = replayed.Assign(Source(), 2, "restricted", 1, "Regulated records", Actor, Now.AddDays(4));

        // Assert
        Assert.Equal("confidential", restoredClass);
        Assert.Equal(2, restoredRevision);
        Assert.True(retry.IsSuccess);
        Assert.True(next.IsSuccess);
        Assert.Equal(3, replayed.Revision);
        Assert.IsType<ArtifactHandlingPolicyRevised>(
            Assert.Single(new AggregateScenario<EvidenceHandlingPolicy>(replayed).PendingEvents));
    }

    [Fact]
    public void ShouldDeriveStableIdentityGivenTenantAndArtifact()
    {
        // Arrange
        var other = Uuid.CreateVersion4();

        // Act
        var first = EvidenceHandlingPolicy.IdFor(TenantId, ArtifactId);
        var again = EvidenceHandlingPolicy.IdFor(TenantId, ArtifactId);
        var otherArtifact = EvidenceHandlingPolicy.IdFor(TenantId, other);
        var otherTenant = EvidenceHandlingPolicy.IdFor(other, ArtifactId);

        // Assert
        Assert.Equal(first, again);
        Assert.Equal(first, NewPolicy().Id);
        Assert.NotEqual(first, otherArtifact);
        Assert.NotEqual(first, otherTenant);
    }

    static EvidenceHandlingPolicy NewPolicy() => new(TenantId, ArtifactId);

    static EvidenceHandlingPolicySourceSnapshot Source() => new(
        new ArtifactRetentionSource(TenantId, "evidence_artifact", ArtifactId, new string('a', 64)),
        12, 4096, "available", "internal");
}
