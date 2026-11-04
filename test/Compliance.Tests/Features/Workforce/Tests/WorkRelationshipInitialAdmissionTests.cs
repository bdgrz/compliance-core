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

public sealed class WorkRelationshipInitialAdmissionTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Person = Uuid.CreateVersion4();
    static readonly DateOnly Start = new(2026, 1, 5);
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Theory]
    [InlineData("pending")]
    [InlineData("active")]
    public void ShouldRecordInitialFactGivenDocumentedInitialStatus(string status)
    {
        // Arrange
        var relationship = New();

        // Act
        var result = relationship.Record(Person, "E-100", Terms(status), Author, Now);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, relationship.Revision);
        var fact = Assert.IsType<WorkRelationshipRecorded>(Assert.Single(
            new AggregateScenario<WorkRelationship>(relationship).PendingEvents));
        Assert.Equal(status, fact.Terms.LifecycleStatus);
    }

    [Theory]
    [InlineData("on_leave", false)]
    [InlineData("ended", false)]
    [InlineData("on_leave", true)]
    [InlineData("ended", true)]
    public void ShouldRefuseWithoutFactGivenNewRelationshipSkipsInitialLifecycle(string status, bool sourced)
    {
        // Arrange
        var relationship = New();
        var source = sourced ? new WorkforceSourceIdentity("hris", "HR", "External-1", "7") : null;

        // Act
        var result = relationship.Record(Person, "E-100", Terms(status), Author, Now, source);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.False(relationship.IsCreated);
        Assert.Equal(0, relationship.Revision);
        Assert.Empty(new AggregateScenario<WorkRelationship>(relationship).PendingEvents);
    }

    [Theory]
    [InlineData("on_leave", false)]
    [InlineData("ended", false)]
    [InlineData("on_leave", true)]
    [InlineData("ended", true)]
    public void ShouldPreserveOriginalRetryWithoutFactGivenHistoricalInitialStatus(string status, bool serialized)
    {
        // Arrange
        var fact = DomainEventSeed.Attach(new WorkRelationshipRecorded(Tenant, New().Id, Person,
            "E-100", Terms(status), Author, Now), New().Id, 1);
        var relationship = NativeFacts.Replay(New(), [fact]);
        if (serialized)
        {
            var restored = (WorkRelationshipRecorded)JsonSerializer.Deserialize(
                JsonSerializer.Serialize(fact, typeof(WorkRelationshipRecorded), ComplianceCoreJsonContext.Default),
                typeof(WorkRelationshipRecorded), ComplianceCoreJsonContext.Default)!;
            relationship = NativeFacts.Replay(New(), [DomainEventSeed.Attach(restored, New().Id, 1)]);
        }
        var revision = relationship.Revision;

        // Act
        var retry = relationship.Record(Person, " e-100 ", Terms(status), Author, Now.AddDays(1));
        var changed = relationship.Record(Person, "E-100", Terms(status) with { Department = "Finance" }, Author, Now);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.Equal(revision, relationship.Revision);
        Assert.Empty(new AggregateScenario<WorkRelationship>(relationship).PendingEvents);
    }

    [Fact]
    public void ShouldPreserveTerminalRefusalGivenHistoricalInitialEnding()
    {
        // Arrange
        var fact = DomainEventSeed.Attach(new WorkRelationshipRecorded(Tenant, New().Id, Person,
            "E-100", Terms("ended"), Author, Now), New().Id, 1);
        var relationship = NativeFacts.Replay(New(), [fact]);

        // Act
        var failure = relationship.Start(1, Start, Author, Now);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(1, relationship.Revision);
        Assert.Empty(new AggregateScenario<WorkRelationship>(relationship).PendingEvents);
    }

    static WorkRelationship New() => new(Tenant, WorkRelationship.IdFor(Tenant, "E-100"));

    static WorkRelationshipTerms Terms(string status) => new("employee", status, Start,
        status == "ended" ? Start.AddMonths(1) : null, "Engineering", null, null);
}
