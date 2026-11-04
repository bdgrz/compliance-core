using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class WorkRelationshipLifecycleTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid PersonId = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Start = new(2026, 1, 5);

    [Fact]
    public void ShouldRecordNamedLifecycleFactsGivenPendingRelationship()
    {
        // Arrange
        var relationship = Recorded("pending");
        var scenario = new AggregateScenario<WorkRelationship>(relationship);
        var actualStart = Start.AddDays(1);

        // Act
        var started = relationship.Start(1, actualStart, Author, Now);
        var leave = relationship.StartLeave(2, Start.AddMonths(1), Author, Now.AddMinutes(1));
        var returned = relationship.EndLeave(3, Start.AddMonths(2), Author, Now.AddMinutes(2));
        var ended = relationship.End(4, Start.AddMonths(3), "Contract completed", Author, Now.AddMinutes(3));

        // Assert
        Assert.Null(started);
        Assert.Null(leave);
        Assert.Null(returned);
        Assert.Null(ended);
        Assert.Equal(5, relationship.Revision);
        var facts = scenario.PendingEvents.Skip(1).ToArray();
        Assert.Collection(facts,
            fact => Assert.Equal(actualStart, Assert.IsType<WorkRelationshipStarted>(fact).StartDate),
            fact => Assert.Equal(Start.AddMonths(1), Assert.IsType<WorkRelationshipLeaveStarted>(fact).FromDate),
            fact => Assert.Equal(Start.AddMonths(2), Assert.IsType<WorkRelationshipLeaveEnded>(fact).ReturnDate),
            fact =>
            {
                var ending = Assert.IsType<WorkRelationshipEnded>(fact);
                Assert.Equal(Start.AddMonths(3), ending.EndDate);
                Assert.Equal("Contract completed", ending.Reason);
            });
        for (var index = 0; index < facts.Length; index++)
        {
            var payload = Identity(facts[index]);
            Assert.Equal(TenantId, payload.Tenant);
            Assert.Equal(relationship.Id, payload.Relationship);
            Assert.Equal("E-100", payload.Worker);
            Assert.Equal(index + 2, payload.Revision);
            Assert.Equal(Author, payload.Actor);
            Assert.Equal(Now.AddMinutes(index), payload.At);
        }
    }

    [Theory]
    [InlineData("start", "active")]
    [InlineData("start", "on_leave")]
    [InlineData("start", "ended")]
    [InlineData("leave", "pending")]
    [InlineData("leave", "on_leave")]
    [InlineData("leave", "ended")]
    [InlineData("return", "pending")]
    [InlineData("return", "active")]
    [InlineData("return", "ended")]
    [InlineData("end", "ended")]
    public void ShouldRefuseWithoutFactGivenWrongLifecycleState(string command, string status)
    {
        // Arrange
        var relationship = Recorded(status);
        var scenario = new AggregateScenario<WorkRelationship>(relationship);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var failure = Apply(relationship, command, 1);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(1, relationship.Revision);
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("leave")]
    [InlineData("return")]
    [InlineData("end")]
    public void ShouldRefuseWithoutFactGivenMissingRelationship(string command)
    {
        // Arrange
        var relationship = New();
        var scenario = new AggregateScenario<WorkRelationship>(relationship);

        // Act
        var failure = Apply(relationship, command, 0);

        // Assert
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Empty(scenario.PendingEvents);
    }

    [Theory]
    [InlineData("start", "pending")]
    [InlineData("leave", "active")]
    [InlineData("return", "on_leave")]
    [InlineData("end", "active")]
    public void ShouldRefuseWithoutFactGivenStaleLifecycleDecision(string command, string status)
    {
        // Arrange
        var relationship = Recorded(status);
        var scenario = new AggregateScenario<WorkRelationship>(relationship);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var failure = Apply(relationship, command, 0);

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(1, relationship.Revision);
        Assert.Equal(before, scenario.PendingEvents);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("active")]
    [InlineData("on_leave")]
    public void ShouldEndWithNamedFactGivenNonterminalRelationship(string status)
    {
        // Arrange
        var relationship = Recorded(status);
        var scenario = new AggregateScenario<WorkRelationship>(relationship);

        // Act
        var failure = relationship.End(1, Start, "Left the organization", Author, Now);

        // Assert
        Assert.Null(failure);
        var fact = Assert.IsType<WorkRelationshipEnded>(scenario.PendingEvents[^1]);
        Assert.Equal(Start, fact.EndDate);
        Assert.Equal("Left the organization", fact.Reason);
        Assert.Equal(2, relationship.Revision);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenEndBeforeActualStart()
    {
        // Arrange
        var relationship = Recorded("pending");
        Assert.Null(relationship.Start(1, Start.AddDays(10), Author, Now));
        var scenario = new AggregateScenario<WorkRelationship>(relationship);
        var before = scenario.PendingEvents.ToArray();

        // Act
        var failure = relationship.End(2, Start.AddDays(9), "Invalid date", Author, Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(before, scenario.PendingEvents);
        Assert.Equal(2, relationship.Revision);
    }

    [Fact]
    public void ShouldPreserveTerminalLifecycleGivenNamedEndingAndLegacyRevision()
    {
        // Arrange
        var relationship = Recorded("active");
        Assert.Null(relationship.End(1, Start, "Ended", Author, Now));
        var facts = NativeFacts.Raised(relationship);
        var legacy = DomainEventSeed.Attach(new WorkRelationshipRevised(TenantId, relationship.Id, 3,
            Terms("pending"), Author, Now), relationship.Id, 3);
        var replayed = NativeFacts.Replay(New(), facts.Append(legacy));
        var scenario = new AggregateScenario<WorkRelationship>(replayed);

        // Act
        var failure = replayed.Start(3, Start, Author, Now);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(3, replayed.Revision);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldResumeNamedLifecycleGivenSerializedHistory()
    {
        // Arrange
        var relationship = Recorded("pending");
        Assert.Null(relationship.Start(1, Start, Author, Now));
        Assert.Null(relationship.StartLeave(2, Start.AddDays(10), Author, Now));
        var replayed = NativeFacts.Replay(New(), NativeFacts.Raised(relationship));
        var scenario = new AggregateScenario<WorkRelationship>(replayed);

        // Act
        var failure = replayed.EndLeave(3, Start.AddDays(20), Author, Now);

        // Assert
        Assert.Null(failure);
        Assert.Equal(4, replayed.Revision);
        Assert.IsType<WorkRelationshipLeaveEnded>(Assert.Single(scenario.PendingEvents));
    }

    [Fact]
    public void ShouldStartAnotherLeaveGivenSerializedNamedReturnFromLeave()
    {
        // Arrange
        var relationship = Recorded("pending");
        Assert.Null(relationship.Start(1, Start, Author, Now));
        Assert.Null(relationship.StartLeave(2, Start.AddDays(10), Author, Now));
        Assert.Null(relationship.EndLeave(3, Start.AddDays(20), Author, Now));
        var replayed = NativeFacts.Replay(New(), NativeFacts.Raised(relationship));
        var scenario = new AggregateScenario<WorkRelationship>(replayed);

        // Act
        var failure = replayed.StartLeave(4, Start.AddDays(30), Author, Now);

        // Assert
        Assert.Null(failure);
        Assert.Equal(5, replayed.Revision);
        Assert.IsType<WorkRelationshipLeaveStarted>(Assert.Single(scenario.PendingEvents));
    }

    static (Uuid Tenant, Uuid Relationship, string Worker, long Revision, ActorReference Actor, DateTimeOffset At)
        Identity(DomainEvent fact) => fact switch
        {
            WorkRelationshipStarted started => (started.TenantId, started.RelationshipId, started.WorkerIdentifier,
                started.Revision, started.Actor, started.ChangedAt),
            WorkRelationshipLeaveStarted leave => (leave.TenantId, leave.RelationshipId, leave.WorkerIdentifier,
                leave.Revision, leave.Actor, leave.ChangedAt),
            WorkRelationshipLeaveEnded returned => (returned.TenantId, returned.RelationshipId, returned.WorkerIdentifier,
                returned.Revision, returned.Actor, returned.ChangedAt),
            WorkRelationshipEnded ended => (ended.TenantId, ended.RelationshipId, ended.WorkerIdentifier,
                ended.Revision, ended.Actor, ended.ChangedAt),
            _ => throw new ArgumentOutOfRangeException(nameof(fact)),
        };

    static WorkRelationship New() => new(TenantId, WorkRelationship.IdFor(TenantId, "E-100"));

    static WorkRelationship Recorded(string status)
    {
        var relationship = New();
        Assert.True(relationship.Record(PersonId, "E-100", Terms(status), Author, Now).IsSuccess);
        return relationship;
    }

    static WorkRelationshipTerms Terms(string status) => new("employee", status, Start,
        status == "ended" ? Start : null, "Engineering", null, null);

    static CommandFailure? Apply(WorkRelationship relationship, string command, long revision) => command switch
    {
        "start" => relationship.Start(revision, Start, Author, Now),
        "leave" => relationship.StartLeave(revision, Start, Author, Now),
        "return" => relationship.EndLeave(revision, Start, Author, Now),
        "end" => relationship.End(revision, Start, "Ended", Author, Now),
        _ => throw new ArgumentOutOfRangeException(nameof(command)),
    };
}
