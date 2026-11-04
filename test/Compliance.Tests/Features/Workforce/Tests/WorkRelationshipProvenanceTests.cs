using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class WorkRelationshipProvenanceTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Person = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly WorkRelationshipTerms Terms = new("employee", "active", new DateOnly(2026, 1, 5),
        null, "Engineering", null, null);
    static readonly WorkforceSourceIdentity Source = new("hris", "PeopleSystem", "Record-A", "Revision-7");

    [Fact]
    public void ShouldRetainSourceProvenanceGivenInitialRecording()
    {
        // Arrange
        var relationship = New();
        var supplied = new WorkforceSourceIdentity(" hris ", " PeopleSystem ", " Record-A ", " Revision-7 ");

        // Act
        var result = relationship.Record(Person, "ORG-44", Terms, Author, Now, supplied);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var fact = Assert.IsType<WorkRelationshipRecorded>(Assert.Single(
            new AggregateScenario<WorkRelationship>(relationship).PendingEvents));
        Assert.Equal(Source, fact.Source);
        Assert.Equal("ORG-44", fact.SourceWorkerId);
        Assert.Equal(Author, fact.Actor);
    }

    [Fact]
    public void ShouldKeepWorkerIdentifierAsIdentityGivenDifferentExternalSourceIdentifiers()
    {
        // Arrange
        var first = New();
        var second = New();

        // Act
        var a = first.Record(Person, "ORG-44", Terms, Author, Now, Source);
        var b = second.Record(Person, " org-44 ", Terms, Author, Now,
            Source with { SourceRecordId = "A completely different external record", SourceRevision = "Revision-100" });

        // Assert
        Assert.True(a.IsSuccess);
        Assert.True(b.IsSuccess);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(WorkRelationship.IdFor(Tenant, "ORG-44"), a.Value.RelationshipId);
    }

    [Theory]
    [InlineData("system")]
    [InlineData("record")]
    [InlineData("revision")]
    [InlineData("record_case")]
    [InlineData("kind")]
    public void ShouldRefuseChangedProvenanceWithoutFactGivenSameWorkerIdentifier(string field)
    {
        // Arrange
        var relationship = New();
        Assert.True(relationship.Record(Person, "ORG-44", Terms, Author, Now, Source).IsSuccess);
        var changed = field switch
        {
            "system" => Source with { SourceSystem = "AnotherSystem" },
            "record" => Source with { SourceRecordId = "Record-B" },
            "revision" => Source with { SourceRevision = "Revision-8" },
            "record_case" => Source with { SourceRecordId = "record-a" },
            "kind" => Source with { SourceKind = "idp" },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        // Act
        var result = relationship.Record(Person, "ORG-44", Terms, Author, Now.AddDays(1), changed);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Single(new AggregateScenario<WorkRelationship>(relationship).PendingEvents);
    }

    [Fact]
    public void ShouldReplayExactSourceWithoutFactGivenLaterNamedChanges()
    {
        // Arrange
        var relationship = New();
        Assert.True(relationship.Record(Person, "ORG-44", Terms, Author, Now, Source).IsSuccess);
        Assert.Null(relationship.ChangeDepartment(1, "Finance", Author, Now));
        var replayed = new AggregateScenario<WorkRelationship>(New()).Given(
            new AggregateScenario<WorkRelationship>(relationship).PendingEvents.ToArray()).Aggregate;

        // Act
        var result = replayed.Record(Person, "ORG-44", Terms, Author, Now.AddDays(1), Source);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, replayed.Revision);
        Assert.Empty(new AggregateScenario<WorkRelationship>(replayed).PendingEvents);
    }

    [Theory]
    [InlineData("system", false)]
    [InlineData("record", false)]
    [InlineData("revision", false)]
    [InlineData("system", true)]
    [InlineData("record", true)]
    [InlineData("revision", true)]
    [InlineData("kind", false)]
    public void ShouldRefuseWithoutFactGivenInvalidSourceIdentity(string field, bool oversized)
    {
        // Arrange
        var relationship = New();
        var value = oversized ? new string('x', field == "record" ? 501 : 201) : " ";
        var invalid = field switch
        {
            "system" => Source with { SourceSystem = value },
            "record" => Source with { SourceRecordId = value },
            "revision" => Source with { SourceRevision = value },
            "kind" => Source with { SourceKind = "provider" },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        // Act
        var result = relationship.Record(Person, "ORG-44", Terms, Author, Now, invalid);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.False(relationship.IsCreated);
        Assert.Empty(new AggregateScenario<WorkRelationship>(relationship).PendingEvents);
    }

    [Theory]
    [InlineData("hris")]
    [InlineData("idp")]
    public async Task ShouldProjectExternalProvenanceWithoutReplacingWorkerIdentifierGivenExternalSource(string kind)
    {
        // Arrange
        var directory = new FitzWorkRelationshipDirectory(new InMemoryKvClient());
        var id = WorkRelationship.IdFor(Tenant, "ORG-44");
        var source = Source with { SourceKind = kind };
        var fact = new WorkRelationshipRecorded(Tenant, id, Person, "ORG-44", Terms, Author, Now, source);

        // Act
        await ApplyAsync(directory, fact);
        var view = await directory.GetAsync(Tenant, id);

        // Assert
        Assert.NotNull(view);
        Assert.Equal(kind, view.SourceKind);
        Assert.Equal(source, view.Source);
        Assert.Equal("ORG-44", view.SourceWorkerId);
        Assert.Equal(id, view.RelationshipId);
    }

    [Fact]
    public async Task ShouldKeepManualSourceGivenNoExternalIdentifier()
    {
        // Arrange
        var directory = new FitzWorkRelationshipDirectory(new InMemoryKvClient());
        var relationship = New();
        Assert.True(relationship.Record(Person, "ORG-44", Terms, Author, Now).IsSuccess);
        var fact = Assert.IsType<WorkRelationshipRecorded>(Assert.Single(
            new AggregateScenario<WorkRelationship>(relationship).PendingEvents));

        // Act
        await ApplyAsync(directory, fact);
        var view = await directory.GetAsync(Tenant, relationship.Id);

        // Assert
        Assert.NotNull(view);
        Assert.Equal("manual", view.SourceKind);
        Assert.Null(view.Source);
    }

    static WorkRelationship New() => new(Tenant, WorkRelationship.IdFor(Tenant, "ORG-44"));

    static async ValueTask ApplyAsync(FitzWorkRelationshipDirectory directory, DomainEvent fact)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("WorkRelationshipDirectoryV1",
                EventStreamPattern.ForPattern(Tenant.ToString(), "work-relationships")), ProjectionCheckpoint.Start));
        await directory.ApplyAsync(fact);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
