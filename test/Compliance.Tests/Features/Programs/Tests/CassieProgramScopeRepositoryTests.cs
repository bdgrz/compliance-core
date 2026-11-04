using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Boundaries.Projections;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Programs.Projections;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Snapshots.Events;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.Snapshots.Readers;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.Programs.Tests;

public sealed class CassieProgramScopeRepositoryTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static IReadOnlyList<string> Schema => [.. CassieProgramSchema.Statements, .. CassieBoundarySchema.Statements, .. CassieSnapshotSchema.Statements];

    [Theory]
    [InlineData("program")]
    [InlineData("service")]
    [InlineData("boundary")]
    [InlineData("snapshot")]
    [InlineData("population")]
    public async Task ShouldKeepRowsAndProgressGivenNativeBatchCommitted(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(Schema);
        await using var source = new BoundaryProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var scenario = await CreateAsync(kind, database.Database, source, tenant);
        await using var repository = scenario.Store;
        var progress = new ProjectionCheckpoint(new EventCursor("program-scope-committed"));

        // Act
        await using (var batch = await repository.BeginAsync(new ProjectionBatchContext(scenario.Identity, ProjectionCheckpoint.Start)))
        {
            await scenario.Apply();
            await batch.CommitAsync(progress);
        }
        var found = await scenario.Exists(tenant);
        var foreign = await scenario.Exists(Uuid.CreateVersion4());
        var checkpoint = await repository.LoadCheckpointAsync(scenario.Identity);

        // Assert
        Assert.True(found);
        Assert.False(foreign);
        Assert.Equal(progress, checkpoint);
    }

    [Theory]
    [InlineData("program")]
    [InlineData("service")]
    [InlineData("boundary")]
    [InlineData("snapshot")]
    [InlineData("population")]
    public async Task ShouldDiscardRowsAndProgressGivenNativeBatchDisposedWithoutCommit(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(Schema);
        await using var source = new BoundaryProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var scenario = await CreateAsync(kind, database.Database, source, tenant);
        await using var repository = scenario.Store;

        // Act
        await using (await repository.BeginAsync(new ProjectionBatchContext(scenario.Identity, ProjectionCheckpoint.Start)))
            await scenario.Apply();

        // Assert
        Assert.False(await scenario.Exists(tenant));
        Assert.Equal(ProjectionCheckpoint.Start, await repository.LoadCheckpointAsync(scenario.Identity));
    }

    [Theory]
    [InlineData("program")]
    [InlineData("service")]
    [InlineData("boundary")]
    [InlineData("snapshot")]
    [InlineData("population")]
    public async Task ShouldKeepCandidateRowsInvisibleGivenRebuildGenerationCommitted(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(Schema);
        await using var source = new BoundaryProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var scenario = await CreateAsync(kind, database.Database, source, tenant);
        await using var repository = scenario.Store;
        var rebuild = new CheckpointIdentity(scenario.Identity.ComponentName, scenario.Identity.Pattern, "candidate");
        var progress = new ProjectionCheckpoint(new EventCursor("candidate-complete"));

        // Act
        await using (var batch = await repository.BeginAsync(new ProjectionBatchContext(rebuild, ProjectionCheckpoint.Start)))
        {
            await scenario.Apply();
            await batch.CommitAsync(progress);
        }

        // Assert
        Assert.False(await scenario.Exists(tenant));
        Assert.Equal(ProjectionCheckpoint.Start, await repository.LoadCheckpointAsync(scenario.Identity));
        Assert.Equal(progress, await repository.LoadCheckpointAsync(rebuild));
    }

    [Theory]
    [InlineData("program")]
    [InlineData("service")]
    [InlineData("boundary")]
    [InlineData("snapshot")]
    [InlineData("population")]
    public async Task ShouldRefuseFactWithoutRowsGivenDifferentBatchTenant(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(Schema);
        await using var source = new BoundaryProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var scenario = await CreateAsync(kind, database.Database, source, tenant);
        await using var repository = scenario.Store;
        var alien = new CheckpointIdentity(scenario.Identity.ComponentName,
            EventStreamPattern.ForPattern(Uuid.CreateVersion4().ToString(), scenario.Identity.Pattern.Area));
        Exception? failure;

        // Act
        await using (await repository.BeginAsync(new ProjectionBatchContext(alien, ProjectionCheckpoint.Start)))
            failure = await Record.ExceptionAsync(scenario.Apply);

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
        Assert.False(await scenario.Exists(tenant));
        Assert.Equal(ProjectionCheckpoint.Start, await repository.LoadCheckpointAsync(alien));
    }

    static async Task<Scenario> CreateAsync(string kind, CassieProjectionDatabase database, BoundaryProjectionScenario source, Uuid tenant)
    {
        var id = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        switch (kind)
        {
            case "program":
                var created = await ProgramSeeds.CreatedAsync(source.Content, tenant, id, "Readiness", new ProgramPlan(null, null, null, null, null, null), actor, "Author", Now);
                var programs = new CassieProgramDirectory(database, source.Content);
                return new(programs, Identity("ProgramDirectory", tenant), async () => await programs.ApplyAsync(created),
                    async scope => await programs.GetAsync(scope, id) is not null);
            case "service":
                var service = new ProductClientServiceRules(tenant, id);
                Assert.True(service.Create(program, "Service", "Purpose", "Owner", actor, "Author", Now).IsSuccess);
                var serviceFacts = await ProductClientServiceSeeds.FactsAsync(source.Content, service);
                var services = new CassieClientServiceDirectory(database, source.Content);
                return new(services, Identity("ClientServiceDirectory", tenant), async () =>
                {
                    foreach (var fact in serviceFacts)
                        await services.ApplyAsync(fact);
                }, async scope => await services.GetAsync(scope, id) is not null);
            case "boundary":
                var boundary = new BoundaryRules(tenant, id);
                Assert.True(boundary.Create(program, Uuid.CreateVersion5(id, "draft-1"), new BoundaryContent("Scope", "readiness", ["security"], []), actor, "Author", Now).IsSuccess);
                var boundaryFacts = await source.PublishAsync(boundary);
                var boundaries = new CassieBoundaryDirectory(database, source.Boundaries);
                return new(boundaries, Identity(CassieBoundaryDirectory.ProjectorName, tenant), async () =>
                {
                    foreach (var fact in boundaryFacts)
                        await boundaries.ApplyAsync(fact);
                }, async scope => await boundaries.GetAsync(scope, id) is not null);
            case "snapshot":
                var manifest = new ProgramScopeManifest(1, tenant, program, 1, new string('a', 64), Uuid.CreateVersion4(), Uuid.CreateVersion4(), new string('b', 64));
                var frozen = new SnapshotFrozen(tenant, id, id, null, program, "scope", manifest, "exact canonical bytes", new string('c', 64), null, actor, "Author", Now);
                var snapshots = new CassieSnapshotDirectory(database);
                return new(snapshots, Identity("SnapshotDirectory", tenant, "snapshots"), async () => await snapshots.ApplyAsync(frozen),
                    async scope => await snapshots.GetAsync(scope, id) is not null);
            case "population":
                var population = new PopulationSnapshotFrozen(tenant, id, id, null, "workforce", [], 0, 0, new string('d', 64), null, ActorReference.ForMember(actor, "Author"), Now);
                var populations = new CassiePopulationSnapshotDirectory(database);
                return new(populations, Identity("PopulationSnapshotDirectoryV2", tenant, "population-snapshots"), async () => await populations.ApplyAsync(population),
                    async scope => (await populations.ListAsync(scope, "workforce", 10, null)).Items.Count == 1);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    static CheckpointIdentity Identity(string name, Uuid tenant, string? area = null) => new(name, EventStreamPattern.ForPattern(tenant.ToString(), area));
    sealed record Scenario(CassieProjectionRepository Store, CheckpointIdentity Identity, Func<Task> Apply, Func<Uuid, Task<bool>> Exists);
}
