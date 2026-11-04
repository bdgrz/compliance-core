using Bdgrz.Compliance.Features.Workforce.Projections;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Bdgrz.Compliance.Features.Workforce.Services;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class WorkforceLifecycleObservationTests
{
    static readonly DateOnly Start = new(2026, 1, 5);
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Theory]
    [InlineData("start", "pending", "active")]
    [InlineData("leave", "active", "on_leave")]
    [InlineData("return", "on_leave", "active")]
    [InlineData("end", "active", "ended")]
    public async Task ShouldUpdateOwnershipStatusGivenNamedLifecycleFact(string kind, string before,
        string after)
    {
        // Arrange
        var directory = new CassieWorkforceObservationDirectory(database.Database);
        var recorded = Recorded(before);
        await ApplyAsync(directory, recorded.TenantId, recorded);

        // Act
        await ApplyAsync(directory, recorded.TenantId, Fact(kind, recorded, 2));
        var statuses = await directory.ListRelationshipStatusesAsync(recorded.TenantId, recorded.PersonId);
        var observations = await directory.ListAsync(recorded.TenantId, null, 50, null);

        // Assert
        Assert.Equal([after], statuses);
        if (kind == "end")
        {
            var leaver = Assert.Single(observations.Items, item => item.Kind == "leaver");
            Assert.Equal(2, leaver.RelationshipRevision);
            Assert.Equal(Start.AddMonths(1), leaver.EffectiveDate);
            Assert.Equal(Author, leaver.ObservedFrom);
            Assert.Equal(Now, leaver.ObservedAt);
            Assert.Empty(leaver.ChangedFields);
            Assert.Contains("owner_relationship_ended", ServiceIdentityAccountability.Evaluate(
                "active", Start.AddYears(2), Start.AddYears(1), "person", statuses, false));
        }
        else
            Assert.Single(observations.Items);
    }

    [Fact]
    public async Task ShouldAcceptLaterRevisionGivenAllNamedLifecycleFacts()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var directory = new CassieWorkforceObservationDirectory(database.Database);
        var recorded = Recorded("pending");
        await ApplyAsync(directory, recorded.TenantId, recorded);

        // Act
        await ApplyAsync(directory, recorded.TenantId,
            Fact("start", recorded, 2), Fact("leave", recorded, 3), Fact("return", recorded, 4),
            Fact("end", recorded, 5),
            new WorkRelationshipRevised(recorded.TenantId, recorded.RelationshipId, 6,
                recorded.Terms with { LifecycleStatus = "ended", EndDate = Start.AddMonths(2) }, Author, Now));
        var statuses = await directory.ListRelationshipStatusesAsync(recorded.TenantId, recorded.PersonId);
        var leavers = await directory.ListAsync(recorded.TenantId, "leaver", 50, null);

        // Assert
        Assert.Equal(["ended"], statuses);
        Assert.Equal(5, Assert.Single(leavers.Items).RelationshipRevision);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("leave")]
    [InlineData("return")]
    [InlineData("end")]
    public async Task ShouldRefuseWithoutChangingOwnershipStatusGivenMissingPredecessor(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var directory = new CassieWorkforceObservationDirectory(database.Database);
        var recorded = Recorded("active");
        await ApplyAsync(directory, recorded.TenantId, recorded);

        // Act
        var failure = await Record.ExceptionAsync(() => ApplyAsync(directory, recorded.TenantId,
            Fact(kind, recorded, 3)).AsTask());

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(["active"], await directory.ListRelationshipStatusesAsync(recorded.TenantId, recorded.PersonId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseWithoutChangingOwnershipStatusGivenMismatchedFactIdentity(bool tenantMismatch)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var directory = new CassieWorkforceObservationDirectory(database.Database);
        var recorded = Recorded("active");
        await ApplyAsync(directory, recorded.TenantId, recorded);
        var ending = new WorkRelationshipEnded(tenantMismatch ? Uuid.CreateVersion4() : recorded.TenantId,
            recorded.RelationshipId, tenantMismatch ? "E-100" : "E-OTHER", 2, Start, "Restricted reason", Author, Now);

        // Act
        var failure = await Record.ExceptionAsync(() => ApplyAsync(directory, recorded.TenantId, ending).AsTask());

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(["active"], await directory.ListRelationshipStatusesAsync(recorded.TenantId, recorded.PersonId));
    }

    static WorkRelationshipRecorded Recorded(string status)
    {
        var tenant = Uuid.CreateVersion4();
        return new WorkRelationshipRecorded(tenant, WorkRelationship.IdFor(tenant, "E-100"),
            Uuid.CreateVersion4(), "E-100", new WorkRelationshipTerms("employee", status, Start,
                null, "Engineering", null, null), Author, Now);
    }

    static DomainEvent Fact(string kind, WorkRelationshipRecorded recorded, long revision) => kind switch
    {
        "start" => new WorkRelationshipStarted(recorded.TenantId, recorded.RelationshipId, "E-100", revision, Start, Author, Now),
        "leave" => new WorkRelationshipLeaveStarted(recorded.TenantId, recorded.RelationshipId, "E-100", revision, Start, Author, Now),
        "return" => new WorkRelationshipLeaveEnded(recorded.TenantId, recorded.RelationshipId, "E-100", revision, Start, Author, Now),
        "end" => new WorkRelationshipEnded(recorded.TenantId, recorded.RelationshipId, "E-100", revision, Start.AddMonths(1), "Restricted reason", Author, Now),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    static async ValueTask ApplyAsync(CassieWorkforceObservationDirectory directory, Uuid tenant,
        params DomainEvent[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("WorkforceObservationsV1",
                EventStreamPattern.ForPattern(tenant.ToString(), "work-relationships")), ProjectionCheckpoint.Start));
        foreach (var fact in events)
            await directory.ApplyAsync(fact);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
