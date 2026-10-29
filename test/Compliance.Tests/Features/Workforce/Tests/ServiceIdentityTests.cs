using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
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
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class ServiceIdentityTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 30);

    static ServiceIdentityTerms Terms(string ownerKind = "person", DateOnly? reviewBy = null,
        string purpose = "Nightly billing export", string status = "active") =>
        new("billing-exporter", "workload", purpose, "production", status, ownerKind, OwnerId,
            reviewBy ?? Today.AddMonths(6));

    static ServiceIdentity New() => new(TenantId, Uuid.CreateVersion4());

    [Fact]
    public void ShouldRecordIdentityGivenOwnerPurposeAndReviewWithinOneYear()
    {
        // Arrange
        var identity = New();

        // Act
        var result = identity.Record(Terms(reviewBy: Today.AddYears(1)), Today, Author, Now);

        // Assert
        Assert.True(result.IsSuccess);
        var recorded = Assert.IsType<ServiceIdentityRecorded>(
            Assert.Single(new AggregateScenario<ServiceIdentity>(identity).PendingEvents));
        Assert.Equal(OwnerId, recorded.Terms.OwnerId);
        Assert.Equal("person", recorded.Terms.OwnerKind);
        Assert.Equal(Author, recorded.Actor);
    }

    [Theory]
    [InlineData(366)]
    [InlineData(0)]
    [InlineData(-10)]
    public void ShouldRejectRecordGivenReviewDateOutsideNextYear(int days)
    {
        // Arrange
        var identity = New();

        // Act
        var result = identity.Record(Terms(reviewBy: Today.AddDays(days)), Today, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Theory]
    [InlineData("group", "Nightly export")]
    [InlineData("role", "Nightly export")]
    [InlineData("person", " ")]
    public void ShouldRejectRecordGivenNonAccountableOwnerOrMissingPurpose(string ownerKind,
        string purpose)
    {
        // Arrange
        var identity = New();

        // Act
        var result = identity.Record(Terms(ownerKind, purpose: purpose), Today, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public void ShouldReplayIdenticalRecordAndConflictGivenDifferentContent()
    {
        // Arrange
        var identity = New();
        Assert.True(identity.Record(Terms(), Today, Author, Now).IsSuccess);

        // Act
        var replay = identity.Record(Terms(), Today, Author, Now);
        var different = identity.Record(Terms("team"), Today, Author, Now);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(different.Error).Kind);
    }

    [Fact]
    public void ShouldReviseOwnerAndRejectStaleGivenRecordedIdentity()
    {
        // Arrange
        var identity = New();
        var missing = New();
        Assert.True(identity.Record(Terms(), Today, Author, Now).IsSuccess);

        // Act
        var moved = identity.Revise(1, Terms("team"), Today, Author, Now.AddMinutes(1));
        var stale = identity.Revise(1, Terms(), Today, Author, Now.AddMinutes(2));
        var absent = missing.Revise(1, Terms(), Today, Author, Now);
        var retired = identity.Revise(2, Terms(status: "retired", reviewBy: Today.AddDays(-1)),
            Today, Author, Now.AddMinutes(3));

        // Assert
        Assert.Null(moved);
        Assert.Null(retired);
        Assert.Equal(3, identity.Revision);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(absent).Code);
    }

    [Theory]
    [InlineData("active", 10, "person", "active", true, new string[0])]
    [InlineData("active", -1, "person", "active", true, new[] { "review_expired" })]
    [InlineData("active", 10, "person", "ended", true, new[] { "owner_relationship_ended" })]
    [InlineData("disabled", 10, "person", null, true, new[] { "owner_not_on_roster" })]
    [InlineData("active", 10, "team", null, false, new[] { "owner_team_deleted" })]
    [InlineData("retired", -30, "team", null, false, new string[0])]
    public void ShouldExplainUnownedWorkGivenOwnerAndReviewState(string lifecycle, int reviewInDays,
        string ownerKind, string? ownerRelationshipStatus, bool teamActive, string[] expected)
    {
        // Arrange
        string[] statuses = ownerRelationshipStatus is null ? [] : [ownerRelationshipStatus];

        // Act
        var reasons = ServiceIdentityAccountability.Evaluate(lifecycle, Today.AddDays(reviewInDays),
            Today, ownerKind, statuses, teamActive);

        // Assert
        Assert.Equal(expected, reasons);
    }

    [Fact]
    public void ShouldRecordExpiryGivenFutureExpiryDate()
    {
        // Arrange
        var identity = New();
        var expiresOn = Today.AddDays(90);

        // Act
        var result = identity.Record(Terms() with { ExpiresOn = expiresOn }, Today, Author, Now);

        // Assert
        Assert.True(result.IsSuccess);
        var recorded = Assert.IsType<ServiceIdentityRecorded>(
            Assert.Single(new AggregateScenario<ServiceIdentity>(identity).PendingEvents));
        Assert.Equal(expiresOn, recorded.Terms.ExpiresOn);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ShouldRejectRecordGivenActiveIdentityAlreadyExpired(int days)
    {
        // Arrange
        var identity = New();

        // Act
        var result = identity.Record(Terms() with { ExpiresOn = Today.AddDays(days) }, Today,
            Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Theory]
    [InlineData("active", 1, false)]
    [InlineData("active", 0, true)]
    [InlineData("disabled", -5, true)]
    [InlineData("retired", -5, false)]
    public void ShouldReportExpiredGivenExpiryDateReached(string lifecycle, int expiresInDays,
        bool expected)
    {
        // Arrange
        var expiresOn = Today.AddDays(expiresInDays);

        // Act
        var expired = ServiceIdentityAccountability.IsExpired(lifecycle, expiresOn, Today);
        var neverExpires = ServiceIdentityAccountability.IsExpired(lifecycle, null, Today);

        // Assert
        Assert.Equal(expected, expired);
        Assert.False(neverExpires);
    }

    [Fact]
    public void ShouldAppendNothingGivenRevisionWithUnchangedTerms()
    {
        // Arrange
        var identity = New();
        Assert.True(identity.Record(Terms(), Today, Author, Now).IsSuccess);

        // Act
        var noOp = identity.Revise(1, Terms(), Today, Author, Now.AddMinutes(1));

        // Assert
        Assert.Null(noOp);
        Assert.Equal(1, identity.Revision);
        Assert.Single(new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
    }

    [Fact]
    public void ShouldAppendNoFactGivenInvalidStaleOrMissingRevision()
    {
        // Arrange
        var identity = New();
        var missing = New();
        Assert.True(identity.Record(Terms(), Today, Author, Now).IsSuccess);

        // Act
        var invalid = identity.Revise(1, Terms("group"), Today, Author, Now.AddMinutes(1));
        var stale = identity.Revise(7, Terms("team"), Today, Author, Now.AddMinutes(2));
        var absent = missing.Revise(1, Terms(), Today, Author, Now);

        // Assert
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(absent).Code);
        Assert.Empty(new AggregateScenario<ServiceIdentity>(missing).PendingEvents);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(invalid).Code);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(1, identity.Revision);
        Assert.Single(new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
    }

    [Fact]
    public void ShouldAcceptRecordRetryGivenLaterRevision()
    {
        // Arrange
        var identity = New();
        Assert.True(identity.Record(Terms(), Today, Author, Now).IsSuccess);
        Assert.Null(identity.Revise(1, Terms("team"), Today, Author, Now.AddMinutes(1)));

        // Act
        var retry = identity.Record(Terms(), Today, Author, Now);
        var changed = identity.Record(Terms("team"), Today, Author, Now);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.Equal(2, identity.Revision);
    }

    [Fact]
    public void ShouldAcknowledgeRecordRetryGivenReviewDateElapsedSinceCommit()
    {
        // Arrange
        var identity = New();
        Assert.True(identity.Record(Terms(reviewBy: Today.AddDays(5)), Today, Author, Now).IsSuccess);
        var later = Today.AddDays(10);

        // Act
        var retry = identity.Record(Terms(reviewBy: Today.AddDays(5)), later, Author, Now);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Single(new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
    }

    [Fact]
    public void ShouldContinueFromReplayedStateGivenRecordedAndRevisedHistory()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var recorded = DomainEventSeed.Attach(
            new ServiceIdentityRecorded(TenantId, id, Terms(), Author, Now), id, 1);
        var revised = DomainEventSeed.Attach(
            new ServiceIdentityRevised(TenantId, id, 2, Terms("team"), Author, Now.AddMinutes(1)), id, 2);

        // Act
        var identity = new AggregateScenario<ServiceIdentity>(new ServiceIdentity(TenantId, id))
            .Given(new DomainEvent[] { recorded, revised }).Aggregate;
        var stale = identity.Revise(1, Terms(), Today, Author, Now.AddMinutes(2));
        var noOp = identity.Revise(2, Terms("team"), Today, Author, Now.AddMinutes(2));
        var next = identity.Revise(2, Terms(status: "disabled"), Today, Author, Now.AddMinutes(2));

        // Assert
        Assert.True(identity.IsCreated);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Null(noOp);
        Assert.Null(next);
        Assert.Equal(3, identity.Revision);
    }
}
