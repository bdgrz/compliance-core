using System.Text.Json;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
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
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Tests;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class WorkRelationshipTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid PersonId = Uuid.CreateVersion4();
    static readonly Uuid ManagerId = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Start = new(2025, 1, 6);

    static WorkRelationshipTerms Employee(string status = "active", DateOnly? end = null) =>
        new("employee", status, Start, end, "Engineering", ManagerId, null);

    static WorkRelationship New(string workerId = "E-100") =>
        new(TenantId, WorkRelationship.IdFor(TenantId, workerId));

    [Fact]
    public void ShouldKeyRelationshipBySourceWorkerIdGivenCaseAndSpacing()
    {
        // Arrange
        var otherTenant = Uuid.CreateVersion4();

        // Act
        var spaced = WorkRelationship.IdFor(TenantId, " E-100 ");
        var lower = WorkRelationship.IdFor(TenantId, "e-100");
        var foreign = WorkRelationship.IdFor(otherTenant, "E-100");

        // Assert
        Assert.Equal(lower, spaced);
        Assert.NotEqual(lower, foreign);
    }

    [Fact]
    public void ShouldReplayIdenticalRecordAndRejectAnotherPersonGivenSameWorkerId()
    {
        // Arrange
        var relationship = New();
        Assert.True(relationship.Record(PersonId, " e-100 ", Employee(), Author, Now).IsSuccess);

        // Act
        var replay = relationship.Record(PersonId, "E-100", Employee(), Author, Now);
        var otherPerson = relationship.Record(Uuid.CreateVersion4(), "E-100", Employee(), Author, Now);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(otherPerson.Error).Kind);
        var recorded = Assert.IsType<WorkRelationshipRecorded>(
            Assert.Single(new AggregateScenario<WorkRelationship>(relationship).PendingEvents));
        Assert.Equal("E-100", recorded.SourceWorkerId);
        Assert.Equal(PersonId, recorded.PersonId);
        Assert.Equal(Author, recorded.Actor);
    }

    [Theory]
    [InlineData("volunteer", "active", null, "employee")]
    [InlineData("employee", "retired", null, "employee")]
    [InlineData("employee", "ended", null, "employee")]
    [InlineData("employee", "active", "2024-12-31", "employee")]
    [InlineData("external_collaborator", "active", null, "external_collaborator")]
    public void ShouldRejectRecordGivenInvalidTerms(string workerType, string status, string? end,
        string _)
    {
        // Arrange
        var relationship = New();
        var terms = new WorkRelationshipTerms(workerType, status, Start,
            end is null ? null : DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture), null, null, null);

        // Act
        var result = relationship.Record(PersonId, "E-100", terms, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public void ShouldRejectRecordGivenSelfManagerOrBlankWorkerId()
    {
        // Arrange
        var relationship = New();

        // Act
        var self = relationship.Record(PersonId, "E-100",
            Employee() with { ManagerPersonId = PersonId }, Author, Now);
        var blank = relationship.Record(PersonId, "  ", Employee(), Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(self.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(blank.Error).Kind);
    }

    [Fact]
    public void ShouldAcceptExternalCollaboratorGivenInternalSponsor()
    {
        // Arrange
        var relationship = New("X-7");
        var terms = new WorkRelationshipTerms("external_collaborator", "active", Start, null, null, null,
            Uuid.CreateVersion4());

        // Act
        var result = relationship.Record(PersonId, "X-7", terms, Author, Now);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ShouldRecordLeaverAndRejectStaleGivenNamedEnding()
    {
        // Arrange
        var relationship = New();
        var missing = New("E-404");
        Assert.True(relationship.Record(PersonId, "E-100", Employee(), Author, Now).IsSuccess);

        // Act
        var ended = relationship.End(1, new DateOnly(2026, 9, 1), "Left the organization", Author, Now.AddMinutes(1));
        var stale = relationship.Start(1, Start, Author, Now.AddMinutes(2));
        var absent = missing.Start(1, Start, Author, Now);

        // Assert
        Assert.Null(ended);
        Assert.Equal(2, relationship.Revision);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(absent).Code);
        var ending = Assert.IsType<WorkRelationshipEnded>(
            new AggregateScenario<WorkRelationship>(relationship).PendingEvents[^1]);
        Assert.Equal(new DateOnly(2026, 9, 1), ending.EndDate);
        Assert.Equal("Left the organization", ending.Reason);
    }

    [Theory]
    [InlineData("pending", false, false)]
    [InlineData("active", false, false)]
    [InlineData("on_leave", false, false)]
    [InlineData("pending", false, true)]
    [InlineData("active", false, true)]
    [InlineData("on_leave", false, true)]
    [InlineData("pending", true, false)]
    [InlineData("active", true, false)]
    [InlineData("on_leave", true, false)]
    [InlineData("pending", true, true)]
    [InlineData("active", true, true)]
    [InlineData("on_leave", true, true)]
    public void ShouldRefuseNamedLifecycleWithoutFactGivenEndedWorkRelationship(string status,
        bool initiallyEnded, bool replayed)
    {
        // Arrange
        var relationship = Ended(initiallyEnded, replayed);
        var revision = relationship.Revision;
        var scenario = new AggregateScenario<WorkRelationship>(relationship);
        var facts = scenario.PendingEvents.ToArray();

        // Act
        var failure = AttemptLifecycle(relationship, status, revision);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(revision, relationship.Revision);
        Assert.Equal(facts, scenario.PendingEvents);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ShouldRecordDateCorrectionGivenEndedWorkRelationship(bool initiallyEnded, bool replayed)
    {
        // Arrange
        var relationship = Ended(initiallyEnded, replayed);
        var revision = relationship.Revision;
        var scenario = new AggregateScenario<WorkRelationship>(relationship);
        var count = scenario.PendingEvents.Count;
        var correctedEnd = new DateOnly(2026, 9, 2);

        // Act
        var failure = relationship.CorrectDates(revision, Start, correctedEnd,
            "Corrected the recorded end date.", Author, Now.AddMinutes(2));

        // Assert
        Assert.Null(failure);
        Assert.Equal(revision + 1, relationship.Revision);
        Assert.Equal(count + 1, scenario.PendingEvents.Count);
        var fact = Assert.IsType<WorkRelationshipDatesCorrected>(scenario.PendingEvents[^1]);
        Assert.Equal(Start, fact.StartDate);
        Assert.Equal(correctedEnd, fact.EndDate);
        Assert.Equal("Corrected the recorded end date.", fact.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldReplayOriginalRegistrationWithoutFactGivenEndedWorkRelationship(bool replayed)
    {
        // Arrange
        var relationship = Ended(false, replayed);
        var revision = relationship.Revision;
        var scenario = new AggregateScenario<WorkRelationship>(relationship);
        var facts = scenario.PendingEvents.ToArray();

        // Act
        var retry = relationship.Record(PersonId, " e-100 ", Employee(), Author, Now);
        var changed = relationship.Record(PersonId, "E-100", Employee("pending"), Author, Now);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.Equal(revision, relationship.Revision);
        Assert.Equal(facts, scenario.PendingEvents);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("active")]
    [InlineData("on_leave")]
    public void ShouldRefuseNewReactivationGivenLegacyReactivatedHistory(string status)
    {
        // Arrange
        var ended = Ended(false, false);
        var legacy = DomainEventSeed.Attach(new WorkRelationshipRevised(TenantId, ended.Id, 3,
            Employee(), Author, Now.AddMinutes(2)), ended.Id, 3);
        var relationship = NativeFacts.Replay(New(), NativeFacts.Raised(ended).Append(legacy));
        var scenario = new AggregateScenario<WorkRelationship>(relationship);
        Assert.Equal(3, relationship.Revision);

        // Act
        var failure = AttemptLifecycle(relationship, status, 3);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(3, relationship.Revision);
        Assert.Empty(scenario.PendingEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldPreserveStaleRefusalWithoutFactGivenEndingDecisionWon(bool replayed)
    {
        // Arrange
        var relationship = Ended(false, replayed);
        var scenario = new AggregateScenario<WorkRelationship>(relationship);
        var facts = scenario.PendingEvents.ToArray();

        // Act
        var failure = relationship.Start(1, Start, Author, Now.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(2, relationship.Revision);
        Assert.Equal(facts, scenario.PendingEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldPreserveContentRefusalWithoutFactGivenInvalidDateCorrectionAfterEnding(bool replayed)
    {
        // Arrange
        var relationship = Ended(false, replayed);
        var scenario = new AggregateScenario<WorkRelationship>(relationship);
        var facts = scenario.PendingEvents.ToArray();

        // Act
        var failure = relationship.CorrectDates(2, Start, Start.AddDays(-1),
            "Incorrect dates", Author, Now.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(2, relationship.Revision);
        Assert.Equal(facts, scenario.PendingEvents);
    }

    [Fact]
    public void ShouldPreserveNonterminalLifecycleGivenRelationshipHasNotEnded()
    {
        // Arrange
        var relationship = New();
        Assert.True(relationship.Record(PersonId, "E-100", Employee("pending"), Author, Now).IsSuccess);
        var scenario = new AggregateScenario<WorkRelationship>(relationship);

        // Act
        var started = relationship.Start(1, Start, Author, Now.AddMinutes(1));
        var leave = relationship.StartLeave(2, Start.AddDays(1), Author, Now.AddMinutes(2));
        var returned = relationship.EndLeave(3, Start.AddDays(2), Author, Now.AddMinutes(3));

        // Assert
        Assert.Null(started);
        Assert.Null(leave);
        Assert.Null(returned);
        Assert.Equal(4, relationship.Revision);
        Assert.Equal(4, scenario.PendingEvents.Count);
        Assert.Collection(scenario.PendingEvents.Skip(1),
            fact => Assert.IsType<WorkRelationshipStarted>(fact),
            fact => Assert.IsType<WorkRelationshipLeaveStarted>(fact),
            fact => Assert.IsType<WorkRelationshipLeaveEnded>(fact));
    }

    static CommandFailure? AttemptLifecycle(WorkRelationship relationship, string status, long revision) => status switch
    {
        "pending" => relationship.Start(revision, Start, Author, Now.AddMinutes(2)),
        "active" => relationship.EndLeave(revision, Start, Author, Now.AddMinutes(2)),
        "on_leave" => relationship.StartLeave(revision, Start, Author, Now.AddMinutes(2)),
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    static WorkRelationship Ended(bool initiallyEnded, bool replayed)
    {
        var relationship = New();
        var ended = Employee("ended", new DateOnly(2026, 9, 1));
        if (initiallyEnded)
        {
            var historical = new WorkRelationshipRecorded(TenantId, relationship.Id, PersonId,
                "E-100", ended, Author, Now);
            if (replayed)
                historical = (WorkRelationshipRecorded)JsonSerializer.Deserialize(
                    JsonSerializer.Serialize(historical, typeof(WorkRelationshipRecorded), ComplianceCoreJsonContext.Default),
                    typeof(WorkRelationshipRecorded), ComplianceCoreJsonContext.Default)!;
            return NativeFacts.Replay(relationship, [DomainEventSeed.Attach(historical, relationship.Id, 1)]);
        }
        Assert.True(relationship.Record(PersonId, "E-100", Employee(), Author, Now).IsSuccess);
        Assert.Null(relationship.End(1, ended.EndDate!.Value, null, Author, Now.AddMinutes(1)));
        return replayed ? NativeFacts.Replay(New(), NativeFacts.Raised(relationship)) : relationship;
    }
}
