using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class ServiceIdentityNamedOperationsTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Person = Uuid.CreateVersion4();
    static readonly Uuid Team = Uuid.CreateVersion4();
    static readonly DateOnly Today = new(2026, 11, 4);
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Reviewer");

    [Fact]
    public void ShouldRecordEachNamedFactWithActorGivenGovernedChanges()
    {
        // Arrange
        var identity = Recorded();

        // Act
        var owner = identity.ChangeOwner(1, "team", Team, Author, Now);
        var purpose = identity.ChangePurpose(2, " Approved new purpose ", Author, Now);
        var reviewed = identity.Review(3, Today.AddYears(1), " Checked the current purpose ", Author, Now);
        var disabled = identity.Disable(4, "Investigation", Author, Now);
        var enabled = identity.Enable(5, Author, Now);
        var retired = identity.Retire(6, "Replaced by a new identity", Author, Now);

        // Assert
        Assert.All(new[] { owner, purpose, reviewed, disabled, enabled, retired }, Assert.Null);
        Assert.Equal(7, identity.Revision);
        Assert.Collection(new AggregateScenario<ServiceIdentity>(identity).PendingEvents.Skip(1),
            fact =>
            {
                var changed = Assert.IsType<ServiceIdentityOwnerChanged>(fact);
                Assert.Equal("person", changed.PreviousOwnerKind);
                Assert.Equal(Person, changed.PreviousOwnerId);
                Assert.Equal("team", changed.OwnerKind);
                Assert.Equal(Team, changed.OwnerId);
                Assert.Equal(Author, changed.Actor);
            },
            fact => Assert.Equal("Approved new purpose", Assert.IsType<ServiceIdentityPurposeChanged>(fact).Purpose),
            fact =>
            {
                var review = Assert.IsType<ServiceIdentityReviewed>(fact);
                Assert.Equal(Author, review.Reviewer);
                Assert.Equal("Checked the current purpose", review.Notes);
                Assert.Equal(Today.AddYears(1), review.NextReviewDate);
                Assert.Equal(Now, review.ReviewedAt);
            },
            fact => Assert.Equal("Investigation", Assert.IsType<ServiceIdentityDisabled>(fact).Reason),
            fact => Assert.Equal(Author, Assert.IsType<ServiceIdentityEnabled>(fact).Actor),
            fact => Assert.Equal("Replaced by a new identity", Assert.IsType<ServiceIdentityRetired>(fact).Reason));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("purpose")]
    [InlineData("review")]
    [InlineData("disable")]
    [InlineData("enable")]
    [InlineData("retire")]
    public void ShouldRefuseWithoutFactGivenMissingNamedOperationOwner(string operation)
    {
        // Arrange
        var identity = New();

        // Act
        var failure = Apply(identity, operation, 0);

        // Assert
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Empty(new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("purpose")]
    [InlineData("review")]
    [InlineData("disable")]
    [InlineData("enable")]
    [InlineData("retire")]
    public void ShouldRefuseWithoutFactGivenStaleNamedOperation(string operation)
    {
        // Arrange
        var identity = Recorded();
        var before = new AggregateScenario<ServiceIdentity>(identity).PendingEvents.ToArray();

        // Act
        var failure = Apply(identity, operation, 0);

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(before, new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
        Assert.Equal(1, identity.Revision);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("purpose")]
    [InlineData("review")]
    [InlineData("disable")]
    [InlineData("enable")]
    [InlineData("retire")]
    public void ShouldRefuseWithoutFactGivenRetiredNamedOperation(string operation)
    {
        // Arrange
        var identity = Recorded("retired");
        var before = new AggregateScenario<ServiceIdentity>(identity).PendingEvents.ToArray();

        // Act
        var failure = Apply(identity, operation, 1);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(before, new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("purpose")]
    public void ShouldAppendNoFactGivenCanonicalNamedValues(string operation)
    {
        // Arrange
        var identity = Recorded();

        // Act
        var failure = operation == "owner"
            ? identity.ChangeOwner(1, "person", Person, Author, Now)
            : identity.ChangePurpose(1, " Approved purpose ", Author, Now);

        // Assert
        Assert.Null(failure);
        Assert.Single(new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
        Assert.Equal(1, identity.Revision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public void ShouldRefuseWithoutFactGivenReviewDateOutsideNextYear(int days)
    {
        // Arrange
        var identity = Recorded();

        // Act
        var failure = identity.Review(1, Today.AddDays(days), null, Author, Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Single(new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
    }

    [Fact]
    public void ShouldRecordAnnualReviewEvenWithSameNextDateGivenNewReview()
    {
        // Arrange
        var identity = Recorded();

        // Act
        var failure = identity.Review(1, Today.AddMonths(6), null, Author, Now);

        // Assert
        Assert.Null(failure);
        Assert.IsType<ServiceIdentityReviewed>(new AggregateScenario<ServiceIdentity>(identity).PendingEvents[^1]);
        Assert.Equal(2, identity.Revision);
    }

    [Theory]
    [InlineData("disable", "disabled")]
    [InlineData("enable", "active")]
    public void ShouldRefuseWithoutFactGivenWrongNamedLifecycleState(string operation, string status)
    {
        // Arrange
        var identity = Recorded(status);

        // Act
        var failure = Apply(identity, operation, 1);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Single(new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("purpose")]
    [InlineData("review")]
    [InlineData("disable")]
    [InlineData("enable")]
    [InlineData("retire")]
    public void ShouldPreserveTerminalLatchGivenLegacyReactivatedHistory(string operation)
    {
        // Arrange
        var identity = New();
        DomainEvent[] facts =
        [
            DomainEventSeed.Attach(new ServiceIdentityRecorded(Tenant, identity.Id, Terms("retired"), Author, Now), identity.Id, 1),
            DomainEventSeed.Attach(new ServiceIdentityRevised(Tenant, identity.Id, 2, Terms("active"), Author, Now), identity.Id, 2),
        ];
        identity = NativeFacts.Replay(identity, facts);

        // Act
        var failure = Apply(identity, operation, 2);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Empty(new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
        Assert.Equal(2, identity.Revision);
    }

    [Fact]
    public void ShouldReplayAllNamedFactsGivenLaterNamedChange()
    {
        // Arrange
        var identity = Recorded();
        Assert.Null(identity.ChangeOwner(1, "team", Team, Author, Now));
        Assert.Null(identity.ChangePurpose(2, "Updated purpose", Author, Now));
        Assert.Null(identity.Review(3, Today.AddYears(1), "Reviewed", Author, Now));
        Assert.Null(identity.Disable(4, "Paused", Author, Now));
        Assert.Null(identity.Enable(5, Author, Now));
        var replayed = NativeFacts.Replay(new ServiceIdentity(Tenant, identity.Id),
            new AggregateScenario<ServiceIdentity>(identity).PendingEvents);

        // Act
        var failure = replayed.Retire(6, "Completed", Author, Now);

        // Assert
        Assert.Null(failure);
        Assert.Equal(7, replayed.Revision);
        Assert.IsType<ServiceIdentityRetired>(Assert.Single(new AggregateScenario<ServiceIdentity>(replayed).PendingEvents));
        var terminal = NativeFacts.Replay(new ServiceIdentity(Tenant, identity.Id),
            new AggregateScenario<ServiceIdentity>(identity).PendingEvents.Concat(
                new AggregateScenario<ServiceIdentity>(replayed).PendingEvents));
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(terminal.Enable(7, Author, Now)).Code);
    }

    [Fact]
    public void ShouldRecordSharedAccountGivenPersonOwnerAndJustification()
    {
        // Arrange
        var identity = New();
        var terms = Terms("active") with { IdentityKind = "shared_account", SharedJustification = " Emergency administration " };

        // Act
        var result = identity.Record(terms, Today, Author, Now);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var fact = Assert.IsType<ServiceIdentityRecorded>(Assert.Single(new AggregateScenario<ServiceIdentity>(identity).PendingEvents));
        Assert.Equal("Emergency administration", fact.Terms.SharedJustification);
    }

    [Theory]
    [InlineData("team", "Required business purpose")]
    [InlineData("person", null)]
    [InlineData("person", " ")]
    public void ShouldRefuseWithoutFactGivenUnaccountableSharedAccount(string ownerKind, string? justification)
    {
        // Arrange
        var identity = New();
        var terms = Terms("active") with { IdentityKind = "shared_account", OwnerKind = ownerKind, SharedJustification = justification };

        // Act
        var result = identity.Record(terms, Today, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
    }

    [Theory]
    [InlineData("owner_kind")]
    [InlineData("owner_id")]
    [InlineData("purpose_blank")]
    [InlineData("purpose_long")]
    public void ShouldRefuseWithoutFactGivenInvalidNamedContent(string invalid)
    {
        // Arrange
        var identity = Recorded();

        // Act
        var failure = invalid switch
        {
            "owner_kind" => identity.ChangeOwner(1, "role", Person, Author, Now),
            "owner_id" => identity.ChangeOwner(1, "person", Uuid.Empty, Author, Now),
            "purpose_blank" => identity.ChangePurpose(1, " ", Author, Now),
            "purpose_long" => identity.ChangePurpose(1, new string('x', 1001), Author, Now),
            _ => throw new ArgumentOutOfRangeException(nameof(invalid)),
        };

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Single(new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
        Assert.Equal(1, identity.Revision);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenSharedAccountOwnerChangedToTeam()
    {
        // Arrange
        var identity = New();
        Assert.True(identity.Record(Terms("active") with
        { IdentityKind = "shared_account", SharedJustification = "Emergency use" }, Today, Author, Now).IsSuccess);

        // Act
        var failure = identity.ChangeOwner(1, "team", Team, Author, Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Single(new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenEnablingExpiredIdentity()
    {
        // Arrange
        var identity = New();
        Assert.True(identity.Record(Terms("disabled") with { ExpiresOn = Today }, Today, Author, Now).IsSuccess);

        // Act
        var failure = identity.Enable(1, Author, Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Single(new AggregateScenario<ServiceIdentity>(identity).PendingEvents);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("purpose")]
    [InlineData("review")]
    [InlineData("disable")]
    [InlineData("retire")]
    public void ShouldAllowNamedRepairGivenReviewDateHasElapsed(string operation)
    {
        // Arrange
        var identity = New();
        Assert.True(identity.Record(Terms("active") with { ReviewBy = Today.AddDays(1) }, Today, Author, Now).IsSuccess);
        var later = Now.AddMonths(2);

        // Act
        var failure = operation switch
        {
            "owner" => identity.ChangeOwner(1, "team", Team, Author, later),
            "purpose" => identity.ChangePurpose(1, "Updated purpose", Author, later),
            "review" => identity.Review(1, Today.AddMonths(6), "Overdue review completed", Author, later),
            "disable" => identity.Disable(1, "Paused", Author, later),
            "retire" => identity.Retire(1, "Replaced", Author, later),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        // Assert
        Assert.Null(failure);
        Assert.Equal(2, identity.Revision);
        Assert.Equal(2, new AggregateScenario<ServiceIdentity>(identity).PendingEvents.Count);
    }

    static ServiceIdentity New() => new(Tenant, Uuid.CreateVersion4());

    static ServiceIdentity Recorded(string status = "active")
    {
        var identity = New();
        Assert.True(identity.Record(Terms(status), Today, Author, Now).IsSuccess);
        return identity;
    }

    static ServiceIdentityTerms Terms(string status) => new("Deployment bot", "bot", "Approved purpose", null,
        status, "person", Person, Today.AddMonths(6));

    static CommandFailure? Apply(ServiceIdentity identity, string operation, long revision) => operation switch
    {
        "owner" => identity.ChangeOwner(revision, "team", Team, Author, Now),
        "purpose" => identity.ChangePurpose(revision, "Updated purpose", Author, Now),
        "review" => identity.Review(revision, Today.AddYears(1), "Reviewed", Author, Now),
        "disable" => identity.Disable(revision, "Paused", Author, Now),
        "enable" => identity.Enable(revision, Author, Now),
        "retire" => identity.Retire(revision, "Completed", Author, Now),
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };
}
