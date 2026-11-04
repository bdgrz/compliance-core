using System.Text.Json;
using Cntryl.Cassie.Commands;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Workforce.Projections;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class CassiePersonDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldUseOnlyUniqueTenantCorrelationGivenNameChangesAndUnlinking()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var directory = new CassiePersonDirectory(database.Database);
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var first = Recorded(tenantId, "Ada");
        var second = Recorded(tenantId, "Other person");
        var actor = first.Actor;

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first);
            await directory.ApplyAsync(second);
            await directory.ApplyAsync(new PersonMembershipCorrelated(tenantId, first.PersonId,
                2, userId, actor, Now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Equal("Ada", await directory.ReadAsync(tenantId, userId));
        Assert.Null(await directory.ReadAsync(Uuid.CreateVersion4(), userId));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new PersonRevised(tenantId, first.PersonId, 3,
                "Ada King", null, actor, Now));
            await directory.ApplyAsync(new PersonMembershipCorrelated(tenantId, second.PersonId,
                2, userId, actor, Now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        Assert.Null(await directory.ReadAsync(tenantId, userId));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new PersonMembershipCorrelated(tenantId, second.PersonId,
                3, null, actor, Now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        Assert.Equal("Ada King", await directory.ReadAsync(tenantId, userId));
    }

    [Fact]
    public async Task ShouldPageByNameAndKeepTenantsSeparateGivenSharedProjection()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var directory = new CassiePersonDirectory(database.Database);
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            foreach (var index in Enumerable.Range(1, 201))
                await directory.ApplyAsync(Recorded(tenantId, $"Person {index:D3}"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var first = await directory.ListAsync(tenantId, 200, null);
        var second = await directory.ListAsync(tenantId, 200, first.NextCursor);
        var alien = await directory.ListAsync(otherTenantId, 200, null);

        // Assert
        Assert.Equal(200, first.Items.Count);
        Assert.Equal("Person 001", first.Items[0].DisplayName);
        Assert.Equal("Person 201", Assert.Single(second.Items).DisplayName);
        Assert.Empty(alien.Items);
        Assert.All(first.Items, item => Assert.Equal("manual", item.SourceKind));
    }

    [Fact]
    public async Task ShouldApplyRevisionAndRejectOutOfOrderRevisionGivenRecordedPerson()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var directory = new CassiePersonDirectory(database.Database);
        var tenantId = Uuid.CreateVersion4();
        var recorded = Recorded(tenantId, "Ada Lovelace");
        var editor = ActorReference.ForMember(Uuid.CreateVersion4(), "Editor");
        PersonRevised revised = new(tenantId, recorded.PersonId, 2, "Ada King",
            "ada@example.com", editor, Now.AddMinutes(1));
        var skipped = revised with { Revision = 4 };

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(recorded);
            await directory.ApplyAsync(revised);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(skipped));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var current = await directory.GetAsync(tenantId, recorded.PersonId);
        var alien = await directory.GetAsync(Uuid.CreateVersion4(), recorded.PersonId);

        // Assert
        Assert.NotNull(current);
        Assert.Equal(2, current.Revision);
        Assert.Equal("Ada King", current.DisplayName);
        Assert.Equal("ada@example.com", current.WorkEmail);
        Assert.Equal(editor, current.LastChangedBy);
        Assert.Null(alien);
    }

    [Fact]
    public async Task ShouldProjectMembershipCorrelationAndKeepItAcrossRevisionGivenRecordedPerson()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var directory = new CassiePersonDirectory(database.Database);
        var tenantId = Uuid.CreateVersion4();
        var recorded = Recorded(tenantId, "Ada Lovelace");
        var editor = ActorReference.ForMember(Uuid.CreateVersion4(), "Editor");
        var userId = Uuid.CreateVersion4();

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(recorded);
            await directory.ApplyAsync(new PersonMembershipCorrelated(tenantId, recorded.PersonId, 2,
                userId, editor, Now.AddMinutes(1)));
            await directory.ApplyAsync(new PersonRevised(tenantId, recorded.PersonId, 3, "Ada King",
                null, editor, Now.AddMinutes(2)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var current = await directory.GetAsync(tenantId, recorded.PersonId);

        // Assert
        Assert.NotNull(current);
        Assert.Equal(3, current.Revision);
        Assert.Equal(userId, current.CorrelatedUserId);
        Assert.Equal(editor, current.LastChangedBy);
    }

    [Fact]
    public async Task ShouldRefuseNativeReplacementWithoutMutationGivenPreviousPersonIdentityDiffers()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var source = new Person(fixture.TenantId, fixture.PersonId).Stream;
        await using var directory = new CassiePersonDirectory(database.Database,
            scope.ServiceProvider.GetRequiredService<PersonReader>());
        var checkpoint = ProjectionCheckpoint.Start;
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(fixture.TenantId), checkpoint)))
        {
            await foreach (var record in events.ReadAsync(source, 0))
            {
                await directory.ApplyAsync(record.Event);
                checkpoint = new ProjectionCheckpoint(record.NextCursor);
            }
            await batch.CommitAsync(checkpoint);
        }
        var previous = Assert.IsType<PersonView>(await directory.GetAsync(fixture.TenantId, fixture.PersonId));
        var foreignPersonId = Uuid.CreateVersion4();
        var poisoned = JsonSerializer.Serialize(previous with { PersonId = foreignPersonId },
            ComplianceCoreJsonContext.Default.PersonView);
        await using (var connection = await database.DataSource.OpenConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE workforce_people SET row_json = @json WHERE model_id = @person";
            _ = command.Parameters.Add(new CassieParameter { ParameterName = "json", Value = poisoned });
            _ = command.Parameters.Add(new CassieParameter { ParameterName = "person", Value = fixture.PersonId.ToString() });
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }
        var changed = await fixture.ExecuteAsync("person_details");
        Assert.True(changed.IsSuccess, changed.Error?.Message);
        PersonDetailsChanged? next = null;
        await foreach (var record in events.ReadAsync(source, 0))
            if (record.Event is PersonDetailsChanged details)
                next = details;
        Assert.NotNull(next);
        Assert.True(next.Changed);
        Assert.Equal(previous.Revision + 1, next.Header.Revision);
        Assert.Equal(fixture.PersonId, next.Header.PersonId);
        Assert.Equal(fixture.TenantId, next.Header.TenantId);
        var verified = await scope.ServiceProvider.GetRequiredService<PersonReader>().ReadFactAsync(next);
        Assert.Equal("Ada King", verified.DisplayName);

        // Act
        await using (await directory.BeginAsync(new ProjectionBatchContext(Identity(fixture.TenantId), checkpoint)))
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyAsync(next));

        // Assert
        Assert.Equal(checkpoint, await directory.LoadCheckpointAsync(fixture.TenantId));
        await using var afterConnection = await database.DataSource.OpenConnectionAsync();
        await using var afterCommand = afterConnection.CreateCommand();
        afterCommand.CommandText = "SELECT row_json FROM workforce_people WHERE model_id = @person";
        _ = afterCommand.Parameters.Add(new CassieParameter { ParameterName = "person", Value = fixture.PersonId.ToString() });
        Assert.Equal(poisoned, Assert.IsType<string>(await afterCommand.ExecuteScalarAsync()));
        var retained = Assert.IsType<PersonView>(await directory.GetAsync(fixture.TenantId, fixture.PersonId));
        Assert.Equal(foreignPersonId, retained.PersonId);
        Assert.Equal(previous.Revision, retained.Revision);
        Assert.Equal(previous.DisplayName, retained.DisplayName);
    }

    static PersonRecorded Recorded(Uuid tenantId, string name) => new(tenantId,
        Uuid.CreateVersion4(), name, null,
        ActorReference.ForMember(Uuid.CreateVersion4(), "Author"), Now);

    static CheckpointIdentity Identity(Uuid tenantId) => new("PersonDirectoryV2",
        EventStreamPattern.ForPattern(tenantId.ToString(), "people"));
}
