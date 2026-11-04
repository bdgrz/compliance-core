using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class WorkRelationshipNamedChangesTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Person = Uuid.CreateVersion4();
    static readonly Uuid Manager = Uuid.CreateVersion4();
    static readonly Uuid Sponsor = Uuid.CreateVersion4();
    static readonly Uuid NewManager = Uuid.CreateVersion4();
    static readonly Uuid NewSponsor = Uuid.CreateVersion4();
    static readonly DateOnly Start = new(2026, 1, 5);
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Fact]
    public void ShouldRecordOnlyNamedChangedFieldsGivenWorkRelationship()
    {
        // Arrange
        var relationship = Recorded();
        var scenario = new AggregateScenario<WorkRelationship>(relationship);

        // Act
        var manager = relationship.ChangeManager(1, NewManager, Author, Now);
        var department = relationship.ChangeDepartment(2, " Finance ", Author, Now);
        var sponsor = relationship.ChangeSponsor(3, NewSponsor, Author, Now);
        var dates = relationship.CorrectDates(4, Start.AddDays(1), null, " Payroll correction ", Author, Now);

        // Assert
        Assert.Null(manager);
        Assert.Null(department);
        Assert.Null(sponsor);
        Assert.Null(dates);
        Assert.Equal(Person, relationship.PersonId);
        Assert.Equal(5, relationship.Revision);
        Assert.Collection(scenario.PendingEvents.Skip(1),
            fact => Assert.Equal(NewManager, Assert.IsType<WorkRelationshipManagerChanged>(fact).ManagerPersonId),
            fact => Assert.Equal("Finance", Assert.IsType<WorkRelationshipDepartmentChanged>(fact).Department),
            fact => Assert.Equal(NewSponsor, Assert.IsType<WorkRelationshipSponsorChanged>(fact).SponsorPersonId),
            fact =>
            {
                var corrected = Assert.IsType<WorkRelationshipDatesCorrected>(fact);
                Assert.Equal(Start.AddDays(1), corrected.StartDate);
                Assert.Null(corrected.EndDate);
                Assert.Equal("Payroll correction", corrected.Reason);
                Assert.Equal(Author, corrected.Actor);
                Assert.Equal(Now, corrected.ChangedAt);
                Assert.Equal("E-100", corrected.WorkerIdentifier);
                Assert.Equal(Tenant, corrected.TenantId);
            });
    }

    [Fact]
    public void ShouldClearManagerGivenOptionalManager()
    {
        // Arrange
        var relationship = Recorded();

        // Act
        var failure = relationship.ChangeManager(1, null, Author, Now);

        // Assert
        Assert.Null(failure);
        Assert.Null(Assert.IsType<WorkRelationshipManagerChanged>(
            new AggregateScenario<WorkRelationship>(relationship).PendingEvents[^1]).ManagerPersonId);
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("sponsor")]
    public void ShouldRefuseWithoutFactGivenSelfReference(string kind)
    {
        // Arrange
        var relationship = Recorded();

        // Act
        var failure = kind == "manager"
            ? relationship.ChangeManager(1, Person, Author, Now)
            : relationship.ChangeSponsor(1, Person, Author, Now);

        // Assert
        AssertRefusal(relationship, failure, CommandFailureCode.InvalidContent);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenEmptyManagerIdentity()
    {
        // Arrange
        var relationship = Recorded();

        // Act
        var failure = relationship.ChangeManager(1, Uuid.Empty, Author, Now);

        // Assert
        AssertRefusal(relationship, failure, CommandFailureCode.InvalidContent);
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("department")]
    public void ShouldRefuseWithoutFactGivenEndedRelationship(string kind)
    {
        // Arrange
        var relationship = Recorded(status: "ended");

        // Act
        var failure = Apply(relationship, kind, 1);

        // Assert
        AssertRefusal(relationship, failure, CommandFailureCode.StateConflict, expectedPending: 0);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenDepartmentTooLong()
    {
        // Arrange
        var relationship = Recorded();

        // Act
        var failure = relationship.ChangeDepartment(1, new string('a', 201), Author, Now);

        // Assert
        AssertRefusal(relationship, failure, CommandFailureCode.InvalidContent);
    }

    [Theory]
    [InlineData("employee")]
    [InlineData("contractor")]
    public void ShouldRefuseWithoutFactGivenSponsorChangeForNonExternalWorker(string workerType)
    {
        // Arrange
        var relationship = Recorded(workerType: workerType);

        // Act
        var failure = relationship.ChangeSponsor(1, NewSponsor, Author, Now);

        // Assert
        AssertRefusal(relationship, failure, CommandFailureCode.StateConflict);
    }

    [Fact]
    public void ShouldChangeSponsorWithoutReactivationGivenEndedExternalCollaborator()
    {
        // Arrange
        var relationship = Recorded(status: "ended");

        // Act
        var changed = relationship.ChangeSponsor(1, NewSponsor, Author, Now);
        var started = relationship.Start(2, Start, Author, Now);

        // Assert
        Assert.Null(changed);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(started).Code);
        Assert.IsType<WorkRelationshipSponsorChanged>(new AggregateScenario<WorkRelationship>(relationship).PendingEvents[^1]);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenEmptySponsor()
    {
        // Arrange
        var relationship = Recorded();

        // Act
        var failure = relationship.ChangeSponsor(1, Uuid.Empty, Author, Now);

        // Assert
        AssertRefusal(relationship, failure, CommandFailureCode.InvalidContent);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ShouldRefuseWithoutFactGivenDateCorrectionWithoutReason(string reason)
    {
        // Arrange
        var relationship = Recorded();

        // Act
        var failure = relationship.CorrectDates(1, Start.AddDays(1), null, reason, Author, Now);

        // Assert
        AssertRefusal(relationship, failure, CommandFailureCode.InvalidContent);
    }

    [Theory]
    [InlineData("active", true)]
    [InlineData("ended", true)]
    [InlineData("ended", false)]
    public void ShouldRefuseWithoutFactGivenDatesDisagreeWithStatus(string status, bool beforeStart)
    {
        // Arrange
        var relationship = Recorded(status: status);

        // Act
        var failure = relationship.CorrectDates(1, Start, beforeStart ? Start.AddDays(-1) : null,
            "Correction", Author, Now);

        // Assert
        AssertRefusal(relationship, failure, CommandFailureCode.InvalidContent,
            expectedPending: status == "ended" ? 0 : 1);
    }

    [Fact]
    public void ShouldCorrectDatesWithoutReactivationGivenEndedRelationship()
    {
        // Arrange
        var relationship = Recorded(status: "ended");

        // Act
        var corrected = relationship.CorrectDates(1, Start.AddDays(1), Start.AddMonths(2), "Correction", Author, Now);
        var restarted = relationship.StartLeave(2, Start.AddMonths(3), Author, Now);

        // Assert
        Assert.Null(corrected);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(restarted).Code);
        Assert.Single(new AggregateScenario<WorkRelationship>(relationship).PendingEvents);
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("department")]
    [InlineData("sponsor")]
    [InlineData("dates")]
    public void ShouldProduceNoFactGivenCanonicalValuesAlreadyMatch(string kind)
    {
        // Arrange
        var relationship = Recorded();

        // Act
        var failure = Apply(relationship, kind, 1, sameValues: true);

        // Assert
        Assert.Null(failure);
        Assert.Single(new AggregateScenario<WorkRelationship>(relationship).PendingEvents);
        Assert.Equal(1, relationship.Revision);
    }

    [Theory]
    [InlineData("manager", false)]
    [InlineData("department", false)]
    [InlineData("sponsor", false)]
    [InlineData("dates", false)]
    [InlineData("manager", true)]
    [InlineData("department", true)]
    [InlineData("sponsor", true)]
    [InlineData("dates", true)]
    public void ShouldRefuseWithoutFactGivenMissingOrStaleOwner(string kind, bool stale)
    {
        // Arrange
        var relationship = stale ? Recorded() : New();
        var scenario = new AggregateScenario<WorkRelationship>(relationship);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var failure = Apply(relationship, kind, 0);

        // Assert
        Assert.Equal(stale ? CommandFailureCode.VersionConflict : CommandFailureCode.MissingRecord,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("department")]
    [InlineData("sponsor")]
    [InlineData("dates")]
    public void ShouldRestoreChangedValuesGivenReplayedNamedFact(string kind)
    {
        // Arrange
        var relationship = Recorded();
        Assert.Null(Apply(relationship, kind, 1));
        var replayed = NativeFacts.Replay(New(), new AggregateScenario<WorkRelationship>(relationship).PendingEvents);

        // Act
        var failure = Apply(replayed, kind, 2);

        // Assert
        Assert.Null(failure);
        Assert.Empty(new AggregateScenario<WorkRelationship>(replayed).PendingEvents);
        Assert.Equal(2, replayed.Revision);
    }

    static WorkRelationship New() => new(Tenant, WorkRelationship.IdFor(Tenant, "E-100"));

    static WorkRelationship Recorded(string status = "active", string workerType = "external_collaborator")
    {
        var relationship = New();
        var terms = new WorkRelationshipTerms(workerType, status,
            Start, status == "ended" ? Start.AddMonths(1) : null, "Engineering", Manager, Sponsor);
        if (status is "pending" or "active")
        {
            Assert.True(relationship.Record(Person, "E-100", terms, Author, Now).IsSuccess);
            return relationship;
        }
        var historical = DomainEventSeed.Attach(new WorkRelationshipRecorded(Tenant, relationship.Id,
            Person, "E-100", terms, Author, Now), relationship.Id, 1);
        return NativeFacts.Replay(relationship, [historical]);
    }

    static void AssertRefusal(WorkRelationship relationship, CommandFailure? failure, CommandFailureCode code,
        int expectedPending = 1)
    {
        Assert.Equal(code, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(expectedPending, new AggregateScenario<WorkRelationship>(relationship).PendingEvents.Count);
        Assert.Equal(1, relationship.Revision);
    }

    static CommandFailure? Apply(WorkRelationship relationship, string kind, long revision,
        bool sameValues = false) => kind switch
        {
            "manager" => relationship.ChangeManager(revision, sameValues ? Manager : NewManager, Author, Now),
            "department" => relationship.ChangeDepartment(revision, sameValues ? " Engineering " : "Finance", Author, Now),
            "sponsor" => relationship.ChangeSponsor(revision, sameValues ? Sponsor : NewSponsor, Author, Now),
            "dates" => relationship.CorrectDates(revision, sameValues ? Start : Start.AddDays(1), null, "Correction", Author, Now),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
