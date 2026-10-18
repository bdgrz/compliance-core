using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Retention;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Retention;

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

    static EvidenceHandlingPolicy NewPolicy() => new(TenantId, ArtifactId);

    static EvidenceHandlingPolicySourceSnapshot Source() => new(
        new ArtifactRetentionSource(TenantId, "evidence_artifact", ArtifactId, new string('a', 64)),
        12, 4096, "available", "internal");
}
