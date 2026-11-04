using Bdgrz.Compliance.Features.Workforce.Projections;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class WorkRelationshipLifecycleProjectionTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly DateOnly Start = new(2026, 1, 5);

    [Fact]
    public async Task ShouldProjectNamedLifecycleGivenRecordedRelationship()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var directory = new CassieWorkRelationshipDirectory(database.Database);
        var recorded = Recorded();
        await ApplyAsync(directory, recorded.TenantId, recorded);
        var actualStart = Start.AddDays(1);

        // Act
        await ApplyAsync(directory, recorded.TenantId,
            new WorkRelationshipStarted(recorded.TenantId, recorded.RelationshipId, "E-100", 2,
                actualStart, Author, Now),
            new WorkRelationshipLeaveStarted(recorded.TenantId, recorded.RelationshipId, "E-100", 3,
                Start.AddMonths(1), Author, Now),
            new WorkRelationshipLeaveEnded(recorded.TenantId, recorded.RelationshipId, "E-100", 4,
                Start.AddMonths(2), Author, Now),
            new WorkRelationshipEnded(recorded.TenantId, recorded.RelationshipId, "E-100", 5,
                Start.AddMonths(3), "Contract completed", Author, Now));
        var view = await directory.GetAsync(recorded.TenantId, recorded.RelationshipId);
        var otherTenant = await directory.GetAsync(Uuid.CreateVersion4(), recorded.RelationshipId);

        // Assert
        Assert.NotNull(view);
        Assert.Equal(5, view.Revision);
        Assert.Equal("ended", view.LifecycleStatus);
        Assert.Equal(actualStart, view.StartDate);
        Assert.Equal(Start.AddMonths(3), view.EndDate);
        Assert.Equal("Contract completed", view.EmploymentStatusReason);
        Assert.Equal(recorded.PersonId, view.PersonId);
        Assert.Equal(recorded.Terms.Department, view.Department);
        Assert.Equal(recorded.Terms.ManagerPersonId, view.ManagerPersonId);
        Assert.Equal(Author, view.LastChangedBy);
        Assert.Equal(Now, view.LastChangedAt);
        Assert.Null(otherTenant);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("leave")]
    [InlineData("return")]
    [InlineData("end")]
    public async Task ShouldRefuseNamedFactGivenMissingPredecessor(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var directory = new CassieWorkRelationshipDirectory(database.Database);
        var recorded = Recorded();
        await ApplyAsync(directory, recorded.TenantId, recorded);
        DomainEvent fact = kind switch
        {
            "start" => new WorkRelationshipStarted(recorded.TenantId, recorded.RelationshipId, "E-100", 3, Start, Author, Now),
            "leave" => new WorkRelationshipLeaveStarted(recorded.TenantId, recorded.RelationshipId, "E-100", 3, Start, Author, Now),
            "return" => new WorkRelationshipLeaveEnded(recorded.TenantId, recorded.RelationshipId, "E-100", 3, Start, Author, Now),
            "end" => new WorkRelationshipEnded(recorded.TenantId, recorded.RelationshipId, "E-100", 3, Start, "Ended", Author, Now),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        // Act
        var failure = await Record.ExceptionAsync(() => ApplyAsync(directory, recorded.TenantId, fact).AsTask());

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(1, (await directory.GetAsync(recorded.TenantId, recorded.RelationshipId))!.Revision);
    }

    static WorkRelationshipRecorded Recorded()
    {
        var tenant = Uuid.CreateVersion4();
        return new WorkRelationshipRecorded(tenant, WorkRelationship.IdFor(tenant, "E-100"),
            Uuid.CreateVersion4(), "E-100", new WorkRelationshipTerms("employee", "pending", Start,
                null, "Engineering", Uuid.CreateVersion4(), null), Author, Now);
    }

    static async ValueTask ApplyAsync(CassieWorkRelationshipDirectory directory, Uuid tenant,
        params DomainEvent[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("WorkRelationshipDirectoryV1",
                EventStreamPattern.ForPattern(tenant.ToString(), "work-relationships")), ProjectionCheckpoint.Start));
        foreach (var fact in events)
            await directory.ApplyAsync(fact);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
