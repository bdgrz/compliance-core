using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class WorkRelationshipLegacyReplayTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Person = Uuid.CreateVersion4();
    static readonly DateOnly Start = new(2026, 1, 5);
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Theory]
    [InlineData("start", false)]
    [InlineData("manager", false)]
    [InlineData("department", false)]
    [InlineData("start", true)]
    [InlineData("manager", true)]
    [InlineData("department", true)]
    public void ShouldRefuseWithoutFactGivenSerializedLegacyEnding(string command, bool legacyReactivated)
    {
        // Arrange
        var relationship = New();
        var initial = Terms("active");
        var facts = new List<DomainEvent>
        {
            new WorkRelationshipRecorded(Tenant, relationship.Id, Person, "E-100", initial, Author, Now),
            new WorkRelationshipRevised(Tenant, relationship.Id, 2, Terms("ended"), Author, Now.AddMinutes(1)),
        };
        if (legacyReactivated)
            facts.Add(new WorkRelationshipRevised(Tenant, relationship.Id, 3, initial, Author, Now.AddMinutes(2)));
        relationship = NativeFacts.Replay(relationship, facts.Select((fact, index) =>
            DomainEventSeed.Attach(RoundTrip(fact), relationship.Id, (ulong)index + 1)));
        var revision = relationship.Revision;

        // Act
        var failure = command switch
        {
            "start" => relationship.Start(revision, Start, Author, Now),
            "manager" => relationship.ChangeManager(revision, Uuid.CreateVersion4(), Author, Now),
            "department" => relationship.ChangeDepartment(revision, "Finance", Author, Now),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(legacyReactivated ? 3 : 2, relationship.Revision);
        Assert.Empty(new AggregateScenario<WorkRelationship>(relationship).PendingEvents);
    }

    [Fact]
    public void ShouldContinueNamedLifecycleGivenSerializedLegacyLeaveRevision()
    {
        // Arrange
        var relationship = New();
        DomainEvent[] facts =
        [
            new WorkRelationshipRecorded(Tenant, relationship.Id, Person, "E-100", Terms("pending"), Author, Now),
            new WorkRelationshipRevised(Tenant, relationship.Id, 2, Terms("on_leave"), Author, Now.AddMinutes(1)),
        ];
        relationship = NativeFacts.Replay(relationship, facts.Select((fact, index) =>
            DomainEventSeed.Attach(RoundTrip(fact), relationship.Id, (ulong)index + 1)));

        // Act
        var failure = relationship.EndLeave(2, Start.AddMonths(1), Author, Now.AddMinutes(2));

        // Assert
        Assert.Null(failure);
        Assert.Equal(3, relationship.Revision);
        var returned = Assert.IsType<WorkRelationshipLeaveEnded>(Assert.Single(
            new AggregateScenario<WorkRelationship>(relationship).PendingEvents));
        Assert.Equal("E-100", returned.WorkerIdentifier);
        Assert.Equal(Author, returned.Actor);
    }

    static DomainEvent RoundTrip(DomainEvent fact) => (DomainEvent)JsonSerializer.Deserialize(
        JsonSerializer.Serialize(fact, fact.GetType(), ComplianceCoreJsonContext.Default), fact.GetType(),
        ComplianceCoreJsonContext.Default)!;

    static WorkRelationship New() => new(Tenant, WorkRelationship.IdFor(Tenant, "E-100"));

    static WorkRelationshipTerms Terms(string status) => new("employee", status, Start,
        status == "ended" ? Start.AddMonths(1) : null, "Engineering", null, null);
}
