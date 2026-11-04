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

public sealed class WorkRelationshipNamedChangeProjectionTests
{
    static readonly DateOnly Start = new(2026, 1, 5);
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly Uuid NewPerson = Uuid.CreateVersion4();

    [Theory]
    [InlineData("manager")]
    [InlineData("department")]
    [InlineData("sponsor")]
    [InlineData("dates")]
    public async Task ShouldProjectNamedChangeWithoutPrivateObservationGivenRecordedRelationship(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var primary = new CassieWorkRelationshipDirectory(database.Database);
        await using var observations = new CassieWorkforceObservationDirectory(database.Database);
        var recorded = Recorded();
        await ApplyAsync(primary, observations, recorded.TenantId, recorded);

        // Act
        await ApplyAsync(primary, observations, recorded.TenantId, Fact(kind, recorded, 2));
        var view = await primary.GetAsync(recorded.TenantId, recorded.RelationshipId);
        var work = await observations.ListAsync(recorded.TenantId, null, 50, null);

        // Assert
        Assert.NotNull(view);
        Assert.Equal(2, view.Revision);
        Assert.Equal("active", view.LifecycleStatus);
        Assert.Equal(Author, view.LastChangedBy);
        Assert.Equal(Now, view.LastChangedAt);
        Assert.Equal(recorded.PersonId, view.PersonId);
        Assert.Equal("E-100", view.SourceWorkerId);
        if (kind == "manager")
            Assert.Equal(NewPerson, view.ManagerPersonId);
        else if (kind == "department")
            Assert.Equal("Finance", view.Department);
        else if (kind == "sponsor")
            Assert.Equal(NewPerson, view.SponsorPersonId);
        else
            Assert.Equal(Start.AddDays(1), view.StartDate);
        if (kind is "department" or "sponsor")
            Assert.Equal([kind], Assert.Single(work.Items, item => item.Kind == "mover").ChangedFields);
        else
            Assert.Single(work.Items);
        Assert.Equal(["active"], await observations.ListRelationshipStatusesAsync(recorded.TenantId, recorded.PersonId));
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("department")]
    [InlineData("sponsor")]
    [InlineData("dates")]
    public async Task ShouldRefuseNamedChangeGivenMissingPredecessor(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var primary = new CassieWorkRelationshipDirectory(database.Database);
        await using var observations = new CassieWorkforceObservationDirectory(database.Database);
        var recorded = Recorded();
        await ApplyAsync(primary, observations, recorded.TenantId, recorded);
        var fact = Fact(kind, recorded, 3);

        // Act
        var primaryFailure = await Record.ExceptionAsync(() => ApplyPrimaryAsync(primary, recorded.TenantId, fact).AsTask());
        var observationFailure = await Record.ExceptionAsync(() => ApplyObservationAsync(observations, recorded.TenantId, fact).AsTask());

        // Assert
        Assert.IsType<InvalidOperationException>(primaryFailure);
        Assert.IsType<InvalidOperationException>(observationFailure);
    }

    [Fact]
    public async Task ShouldPreserveEndingReasonWithoutAnotherLeaverGivenDateCorrection()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var primary = new CassieWorkRelationshipDirectory(database.Database);
        await using var observations = new CassieWorkforceObservationDirectory(database.Database);
        var recorded = Recorded("ended");
        await ApplyAsync(primary, observations, recorded.TenantId, recorded);

        // Act
        await ApplyAsync(primary, observations, recorded.TenantId,
            new WorkRelationshipDatesCorrected(recorded.TenantId, recorded.RelationshipId, "E-100", 2,
                Start, Start.AddMonths(2), "Private correction context", Author, Now));
        var view = await primary.GetAsync(recorded.TenantId, recorded.RelationshipId);
        var work = await observations.ListAsync(recorded.TenantId, null, 50, null);

        // Assert
        Assert.NotNull(view);
        Assert.Equal("ended", view.LifecycleStatus);
        Assert.Equal(Start.AddMonths(2), view.EndDate);
        Assert.Equal("Contract completed", view.EmploymentStatusReason);
        Assert.Equal("leaver", Assert.Single(work.Items).Kind);
    }

    [Theory]
    [InlineData("employee", "active", true)]
    [InlineData("contractor", "active", true)]
    [InlineData("external_collaborator", "active", false)]
    [InlineData("employee", "on_leave", false)]
    [InlineData("employee", "ended", false)]
    public async Task ShouldFindEligibleSponsorGivenRelationshipTerms(string workerType, string status, bool expected)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var directory = new CassieWorkforceObservationDirectory(database.Database);
        var original = Recorded(status);
        var recorded = original with { Terms = original.Terms with { WorkerType = workerType } };
        await ApplyObservationAsync(directory, recorded.TenantId, recorded);

        // Act
        var active = await directory.HasActiveInternalRelationshipAsync(recorded.TenantId, recorded.PersonId);
        var otherTenant = await directory.HasActiveInternalRelationshipAsync(Uuid.CreateVersion4(), recorded.PersonId);

        // Assert
        Assert.Equal(expected, active);
        Assert.False(otherTenant);
    }

    static WorkRelationshipRecorded Recorded(string status = "active")
    {
        var tenant = Uuid.CreateVersion4();
        return new WorkRelationshipRecorded(tenant, WorkRelationship.IdFor(tenant, "E-100"),
            Uuid.CreateVersion4(), "E-100", new WorkRelationshipTerms("external_collaborator", status,
                Start, status == "ended" ? Start.AddMonths(1) : null, "Engineering", Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), status == "ended" ? "Contract completed" : null), Author, Now);
    }

    static DomainEvent Fact(string kind, WorkRelationshipRecorded recorded, long revision) => kind switch
    {
        "manager" => new WorkRelationshipManagerChanged(recorded.TenantId, recorded.RelationshipId, "E-100", revision, NewPerson, Author, Now),
        "department" => new WorkRelationshipDepartmentChanged(recorded.TenantId, recorded.RelationshipId, "E-100", revision, "Finance", Author, Now),
        "sponsor" => new WorkRelationshipSponsorChanged(recorded.TenantId, recorded.RelationshipId, "E-100", revision, NewPerson, Author, Now),
        "dates" => new WorkRelationshipDatesCorrected(recorded.TenantId, recorded.RelationshipId, "E-100", revision, Start.AddDays(1), null, "Correction", Author, Now),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    static async ValueTask ApplyAsync(CassieWorkRelationshipDirectory primary,
        CassieWorkforceObservationDirectory observations, Uuid tenant, DomainEvent fact)
    {
        await ApplyPrimaryAsync(primary, tenant, fact);
        await ApplyObservationAsync(observations, tenant, fact);
    }

    static async ValueTask ApplyPrimaryAsync(CassieWorkRelationshipDirectory directory, Uuid tenant, DomainEvent fact)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("WorkRelationshipDirectoryV1",
                EventStreamPattern.ForPattern(tenant.ToString(), "work-relationships")), ProjectionCheckpoint.Start));
        await directory.ApplyAsync(fact);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static async ValueTask ApplyObservationAsync(CassieWorkforceObservationDirectory directory, Uuid tenant, DomainEvent fact)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("WorkforceObservationsV1",
                EventStreamPattern.ForPattern(tenant.ToString(), "work-relationships")), ProjectionCheckpoint.Start));
        await directory.ApplyAsync(fact);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
