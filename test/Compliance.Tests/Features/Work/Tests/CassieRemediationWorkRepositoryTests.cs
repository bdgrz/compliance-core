using Cntryl.Cassie.Commands;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

[Trait("Category", "CassieIntegration")]
public sealed class CassieRemediationWorkRepositoryTests
{
    [Fact]
    public async Task ShouldReadCommittedWorkGivenRepositoryRecreated()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync();
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var finding = await CorrectiveActionChangeWorkflowTests.OpenAsync(fixture);
        var reader = scope.ServiceProvider.GetRequiredService<FindingReader>();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        await using (var writer = new CassieCorrectiveActionWorkItemDirectory(database.Database, reader, TimeProvider.System))
            await ProjectAsync(writer, CassieCorrectiveActionWorkItemDirectory.ProjectorName, writer.SourcePattern(fixture.TenantId),
                ProjectionCheckpoint.Start, events, writer.ApplyAsync);

        // Act
        await using var repository = new CassieCorrectiveActionWorkItemDirectory(database.Database, reader, TimeProvider.System);
        var work = await repository.LoadProgramAsync(fixture.TenantId, fixture.ProgramId);
        var otherProgram = await repository.LoadProjectedProgramAsync(fixture.TenantId, Uuid.CreateVersion4());
        var otherTenant = await repository.LoadProjectedProgramAsync(Uuid.CreateVersion4(), fixture.ProgramId);

        // Assert
        Assert.True(work.IsSuccess, work.Error?.Message);
        Assert.Equal(Assert.Single(finding.CorrectiveActions).Description, Assert.Single(work.Value).Summary);
        Assert.Empty(otherProgram.Value);
        Assert.Empty(otherTenant.Value);
        Assert.True(await repository.LoadRevisionAsync(fixture.TenantId) > 0);
        Assert.NotEqual(ProjectionCheckpoint.Start, await repository.LoadCheckpointAsync(fixture.TenantId));
    }

    [Fact]
    public async Task ShouldRollbackRowsRevisionAndCheckpointGivenProjectionBatchDiscarded()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync();
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        await CorrectiveActionChangeWorkflowTests.OpenAsync(fixture);
        await using var repository = new CassieCorrectiveActionWorkItemDirectory(database.Database,
            scope.ServiceProvider.GetRequiredService<FindingReader>(), TimeProvider.System);
        var pattern = repository.SourcePattern(fixture.TenantId);

        // Act
        await using (var batch = await repository.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(
            CassieCorrectiveActionWorkItemDirectory.ProjectorName, pattern), ProjectionCheckpoint.Start)))
            await ApplySourceAsync(scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), pattern, ProjectionCheckpoint.Start, repository.ApplyAsync);

        // Assert
        Assert.Empty((await repository.LoadProjectedProgramAsync(fixture.TenantId, fixture.ProgramId)).Value);
        Assert.Equal(0, await repository.LoadRevisionAsync(fixture.TenantId));
        Assert.Equal(ProjectionCheckpoint.Start, await repository.LoadCheckpointAsync(fixture.TenantId));
    }

    [Fact]
    public async Task ShouldKeepRebuildRowsSeparateGivenCandidateGenerationProjected()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync();
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        await CorrectiveActionChangeWorkflowTests.OpenAsync(fixture);
        await using var repository = new CassieCorrectiveActionWorkItemDirectory(database.Database,
            scope.ServiceProvider.GetRequiredService<FindingReader>(), TimeProvider.System);
        var pattern = repository.SourcePattern(fixture.TenantId);
        var candidate = new CheckpointIdentity(CassieCorrectiveActionWorkItemDirectory.ProjectorName, pattern, "candidate");
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();

        // Act
        await using (var batch = await repository.BeginAsync(new ProjectionBatchContext(candidate, ProjectionCheckpoint.Start)))
        {
            var cursor = await ApplySourceAsync(events, pattern, ProjectionCheckpoint.Start, repository.ApplyAsync);
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }
        var published = await repository.LoadProjectedProgramAsync(fixture.TenantId, fixture.ProgramId);
        var publishedRevision = await repository.LoadRevisionAsync(fixture.TenantId);
        var candidateCheckpoint = await ((IProjectionStore)repository).LoadCheckpointAsync(candidate);
        await ProjectAsync(repository, CassieCorrectiveActionWorkItemDirectory.ProjectorName, pattern,
            ProjectionCheckpoint.Start, events, repository.ApplyAsync);

        // Assert
        Assert.Empty(published.Value);
        Assert.Equal(0, publishedRevision);
        Assert.NotEqual(ProjectionCheckpoint.Start, candidateCheckpoint);
        Assert.Single((await repository.LoadProgramAsync(fixture.TenantId, fixture.ProgramId)).Value);
    }

    [Fact]
    public async Task ShouldRefuseCrossTenantWriteGivenBatchBoundToAnotherTenant()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync();
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var owner = await fixture.FindingStreamAsync(finding.FindingId);
        await using var repository = new CassieFindingClosureWorkItemDirectory(database.Database,
            scope.ServiceProvider.GetRequiredService<FindingReader>(), TimeProvider.System);
        var otherTenant = Uuid.CreateVersion4();

        // Act
        await using (var batch = await repository.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(
            CassieFindingClosureWorkItemDirectory.ProjectorName, repository.SourcePattern(otherTenant)), ProjectionCheckpoint.Start)))
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await repository.ApplyAsync(owner.Raised!));

        // Assert
        Assert.Empty((await repository.LoadProjectedProgramAsync(fixture.TenantId, fixture.ProgramId)).Value);
        Assert.Empty((await repository.LoadProjectedProgramAsync(otherTenant, fixture.ProgramId)).Value);
    }

    [Fact]
    public async Task ShouldRefreshOwnedWorkGivenCorrectiveActionChanged()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync();
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var finding = await CorrectiveActionChangeWorkflowTests.OpenAsync(fixture);
        await using var repository = new CassieCorrectiveActionWorkItemDirectory(database.Database,
            scope.ServiceProvider.GetRequiredService<FindingReader>(), TimeProvider.System);
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var pattern = repository.SourcePattern(fixture.TenantId);
        await ProjectAsync(repository, CassieCorrectiveActionWorkItemDirectory.ProjectorName, pattern,
            ProjectionCheckpoint.Start, events, repository.ApplyAsync);
        var checkpoint = await repository.LoadCheckpointAsync(fixture.TenantId);
        var before = Assert.Single((await repository.LoadProgramAsync(fixture.TenantId, fixture.ProgramId)).Value);
        var command = CorrectiveActionChangeWorkflowTests.Change(fixture, finding);

        // Act
        await fixture.AsAsync(fixture.LeadUserId, command);
        await ProjectAsync(repository, CassieCorrectiveActionWorkItemDirectory.ProjectorName, pattern,
            checkpoint, events, repository.ApplyAsync);
        var result = await repository.LoadProgramAsync(fixture.TenantId, fixture.ProgramId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var current = Assert.Single(result.Value);
        Assert.Equal(before.WorkItemId, current.WorkItemId);
        Assert.Equal(command.Description.Trim(), current.Summary);
        Assert.Equal(command.OwnerMemberId, current.Responsible.Id);
        Assert.Equal(command.DueOn, current.DueOn);
    }

    [Fact]
    public async Task ShouldReplaceActionWorkWithClosureWorkGivenCancellationCommitted()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync();
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var finding = await CorrectiveActionChangeWorkflowTests.OpenAsync(fixture);
        var reader = scope.ServiceProvider.GetRequiredService<FindingReader>();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        await using var work = new CassieCorrectiveActionWorkItemDirectory(database.Database, reader, TimeProvider.System);
        await using var closures = new CassieFindingClosureWorkItemDirectory(database.Database, reader, TimeProvider.System);
        var pattern = work.SourcePattern(fixture.TenantId);
        await ProjectAsync(work, CassieCorrectiveActionWorkItemDirectory.ProjectorName, pattern,
            ProjectionCheckpoint.Start, events, work.ApplyAsync);
        await ProjectAsync(closures, CassieFindingClosureWorkItemDirectory.ProjectorName, pattern,
            ProjectionCheckpoint.Start, events, closures.ApplyAsync);
        var workCheckpoint = await work.LoadCheckpointAsync(fixture.TenantId);
        var closureCheckpoint = await closures.LoadCheckpointAsync(fixture.TenantId);
        var action = Assert.Single(finding.CorrectiveActions);

        // Act
        await fixture.AsAsync(fixture.LeadUserId, new CancelCorrectiveAction(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, finding.FindingId, action.Revision, action.ActionId, "Duplicate; retained work covers it."));
        await ProjectAsync(work, CassieCorrectiveActionWorkItemDirectory.ProjectorName, pattern, workCheckpoint, events, work.ApplyAsync);
        await ProjectAsync(closures, CassieFindingClosureWorkItemDirectory.ProjectorName, pattern, closureCheckpoint, events, closures.ApplyAsync);

        // Assert
        Assert.Empty((await work.LoadProgramAsync(fixture.TenantId, fixture.ProgramId)).Value);
        var closure = Assert.Single((await closures.LoadProgramAsync(fixture.TenantId, fixture.ProgramId)).Value);
        Assert.Equal(FindingClosureWork.Kind, closure.Kind);
        Assert.Contains(fixture.LeadMemberId, closure.Excluded);
        Assert.Contains(fixture.BackupMemberId, closure.Excluded);
        Assert.DoesNotContain(fixture.ApproverMemberId, closure.Excluded);
    }

    [Fact]
    public async Task ShouldRefusePoisonedRowGivenPayloadNamesAnotherTenant()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync();
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        await CorrectiveActionChangeWorkflowTests.OpenAsync(fixture);
        await using var repository = new CassieCorrectiveActionWorkItemDirectory(database.Database,
            scope.ServiceProvider.GetRequiredService<FindingReader>(), TimeProvider.System);
        await ProjectAsync(repository, CassieCorrectiveActionWorkItemDirectory.ProjectorName,
            repository.SourcePattern(fixture.TenantId), ProjectionCheckpoint.Start,
            scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), repository.ApplyAsync);
        await using var connection = await database.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM work_corrective_actions";
        var original = Assert.IsType<string>(await command.ExecuteScalarAsync());
        var poisoned = original.Replace(fixture.TenantId.ToString(), Uuid.CreateVersion4().ToString(), StringComparison.Ordinal);
        Assert.NotEqual(original, poisoned);
        command.CommandText = "UPDATE work_corrective_actions SET payload = @payload";
        _ = command.Parameters.Add(new CassieParameter { ParameterName = "payload", Value = poisoned });
        Assert.Equal(1, await command.ExecuteNonQueryAsync());

        // Act
        var result = await repository.LoadProjectedProgramAsync(fixture.TenantId, fixture.ProgramId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldPreservePoisonedRowAndCheckpointGivenRefreshFindsPreviousScopeMismatch(bool closure, bool program)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync();
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await using var scope = provider.CreateAsyncScope();
        var finding = await CorrectiveActionChangeWorkflowTests.OpenAsync(fixture);
        var reader = scope.ServiceProvider.GetRequiredService<FindingReader>();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        await using var work = new CassieCorrectiveActionWorkItemDirectory(database.Database, reader, TimeProvider.System);
        await using var closures = new CassieFindingClosureWorkItemDirectory(database.Database, reader, TimeProvider.System);
        IProjectionStore repository = closure ? closures : work;
        Func<DomainEvent, CancellationToken, ValueTask> apply = closure ? closures.ApplyAsync : work.ApplyAsync;
        var name = closure ? CassieFindingClosureWorkItemDirectory.ProjectorName : CassieCorrectiveActionWorkItemDirectory.ProjectorName;
        var table = closure ? "work_finding_closures" : "work_corrective_actions";
        var pattern = work.SourcePattern(fixture.TenantId);
        var identity = new CheckpointIdentity(name, pattern);
        await ProjectAsync(repository, name, pattern, ProjectionCheckpoint.Start, events, apply);
        var checkpoint = await repository.LoadCheckpointAsync(identity);
        var revision = await work.LoadRevisionAsync(fixture.TenantId);
        var original = await ReadPayloadAsync(database, table);
        var oldId = program ? fixture.ProgramId : fixture.TenantId;
        var poisoned = original.Replace(oldId.ToString(), Uuid.CreateVersion4().ToString(), StringComparison.Ordinal);
        Assert.NotEqual(original, poisoned);
        await using (var connection = await database.DataSource.OpenConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"UPDATE {table} SET payload = @payload";
            _ = command.Parameters.Add(new CassieParameter { ParameterName = "payload", Value = poisoned });
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }
        var retained = await ReadPayloadAsync(database, table);
        await fixture.AsAsync(fixture.LeadUserId, CorrectiveActionChangeWorkflowTests.Change(fixture, finding));

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProjectAsync(repository, name, pattern, checkpoint, events, apply));

        // Assert
        Assert.Equal(retained, await ReadPayloadAsync(database, table));
        Assert.Equal(checkpoint, await repository.LoadCheckpointAsync(identity));
        Assert.Equal(revision, await work.LoadRevisionAsync(fixture.TenantId));
    }

    static async Task<string> ReadPayloadAsync(CassieRepositoryTestDatabase database, string table)
    {
        await using var connection = await database.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT payload FROM {table}";
        return Assert.IsType<string>(await command.ExecuteScalarAsync());
    }

    static async Task ProjectAsync(IProjectionStore repository, string name, EventStreamPattern pattern,
        ProjectionCheckpoint checkpoint, IDomainEventReader events, Func<DomainEvent, CancellationToken, ValueTask> apply)
    {
        await using var batch = await repository.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(name, pattern), checkpoint));
        var cursor = await ApplySourceAsync(events, pattern, checkpoint, apply);
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }

    static async Task<EventCursor> ApplySourceAsync(IDomainEventReader events, EventStreamPattern pattern,
        ProjectionCheckpoint checkpoint, Func<DomainEvent, CancellationToken, ValueTask> apply)
    {
        var cursor = checkpoint.Cursor;
        await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
        {
            await apply(record.Event, CancellationToken.None);
            cursor = record.NextCursor;
        }
        return cursor;
    }
}
