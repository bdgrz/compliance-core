using Cntryl.Portia;
using Cntryl.Cassie.Commands;
using Bdgrz.Compliance.Features.Content.Services;
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

    [Theory]
    [InlineData("program")]
    [InlineData("service")]
    [InlineData("boundary")]
    [InlineData("snapshot")]
    [InlineData("population")]
    public async Task ShouldPageEachIdentityOnceGivenTiedSortValuesAndAdjacentSnapshotTicks(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(Schema);
        await using var source = new BoundaryProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        await using var first = await CreateAsync(kind, database.Database, source, tenant, program, Now);
        await using var second = await CreateAsync(kind, database.Database, source, tenant, program, Now);
        await using var third = await CreateAsync(kind, database.Database, source, tenant, program, Now.AddTicks(1));
        Scenario[] scenarios = [first, second, third];
        foreach (var scenario in scenarios)
        {
            var progress = await scenario.Store.LoadCheckpointAsync(scenario.Identity);
            await using var batch = await scenario.Store.BeginAsync(new ProjectionBatchContext(scenario.Identity, progress));
            await scenario.Apply();
            await batch.CommitAsync(new ProjectionCheckpoint(new EventCursor(scenario.Id.ToString())));
        }
        var tied = new[] { first.Id, second.Id }.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray();
        var expected = kind switch
        {
            "snapshot" => tied.Append(third.Id).ToArray(),
            "population" => new[] { third.Id }.Concat(tied).ToArray(),
            _ => scenarios.Select(item => item.Id).OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray()
        };

        // Act
        var actual = new List<Uuid>();
        string? cursor = null;
        for (var index = 0; index <= scenarios.Length; index++)
        {
            var page = await first.ListAsync(tenant, 1, cursor);
            actual.AddRange(page.Items);
            cursor = page.NextCursor;
            if (cursor is null)
                break;
        }

        // Assert
        Assert.Equal(expected, actual);
        Assert.Equal(expected.Length, actual.Distinct().Count());
        Assert.Null(cursor);
    }

    [Theory]
    [InlineData("program")]
    [InlineData("service")]
    [InlineData("boundary")]
    [InlineData("snapshot")]
    [InlineData("population")]
    public async Task ShouldRejectContinuationGivenCursorFromAnotherTenant(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(Schema);
        await using var source = new BoundaryProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        await using var first = await CreateAsync(kind, database.Database, source, tenant, program);
        await using var second = await CreateAsync(kind, database.Database, source, tenant, program);
        foreach (var scenario in new[] { first, second })
        {
            var progress = await scenario.Store.LoadCheckpointAsync(scenario.Identity);
            await using var batch = await scenario.Store.BeginAsync(new ProjectionBatchContext(scenario.Identity, progress));
            await scenario.Apply();
            await batch.CommitAsync(new ProjectionCheckpoint(new EventCursor(scenario.Id.ToString())));
        }
        var page = await first.ListAsync(tenant, 1, null);
        Assert.NotNull(page.NextCursor);

        // Act
        var failure = await Record.ExceptionAsync(() => first.ListAsync(Uuid.CreateVersion4(), 1, page.NextCursor));

        // Assert
        Assert.IsType<ProgramScopeDirectoryQueryException>(failure);
    }

    public static IEnumerable<object[]> CorruptRows()
    {
        foreach (var kind in new[] { "program", "service", "boundary", "snapshot", "population" })
            foreach (var corruption in new[] { "row_id", "scope_id", "sort_key", "tenant", "identity" })
                yield return [kind, corruption];
    }

    [Theory]
    [MemberData(nameof(CorruptRows))]
    public async Task ShouldRefuseStoredRowWithoutChangingProgressGivenBodyOrIndexIdentityMismatch(string kind, string corruption)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(Schema);
        await using var source = new BoundaryProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        await using var scenario = await CreateAsync(kind, database.Database, source, tenant);
        var progress = new ProjectionCheckpoint(new EventCursor("before-corruption"));
        await using (var batch = await scenario.Store.BeginAsync(new ProjectionBatchContext(scenario.Identity, ProjectionCheckpoint.Start)))
        {
            await scenario.Apply();
            await batch.CommitAsync(progress);
        }
        var table = CurrentTable(kind);
        var before = await StoredRowAsync(database, table);
        var after = corruption switch
        {
            "row_id" => before with { Id = Uuid.CreateVersion4().ToString() },
            "scope_id" => before with { Scope = "another-scope" },
            "sort_key" => before with { Sort = "another-sort" },
            "tenant" => before with { Payload = before.Payload.Replace(tenant.ToString(), Uuid.CreateVersion4().ToString(), StringComparison.Ordinal) },
            "identity" => before with { Payload = before.Payload.Replace(scenario.Id.ToString(), Uuid.CreateVersion4().ToString(), StringComparison.Ordinal) },
            _ => throw new ArgumentOutOfRangeException(nameof(corruption)),
        };
        await ReplaceStoredRowAsync(database, table, after);

        // Act
        bool? found = null;
        var pointFailure = await Record.ExceptionAsync(async () => found = await scenario.Exists(tenant));
        Page<Uuid>? visible = null;
        var listFailure = await Record.ExceptionAsync(async () => visible = await scenario.ListAsync(tenant, 10, null));
        await ReplaceStoredRowAsync(database, table, before);

        // Assert
        if (kind == "population" && corruption == "scope_id")
        {
            Assert.Null(pointFailure);
            Assert.False(found);
        }
        else
            Assert.IsType<ImmutableContentIntegrityException>(pointFailure);
        if (corruption == "scope_id" && kind != "program")
        {
            Assert.Null(listFailure);
            Assert.NotNull(visible);
            Assert.Empty(visible.Items);
        }
        else
            Assert.IsType<ImmutableContentIntegrityException>(listFailure);
        Assert.True(await scenario.Exists(tenant));
        Assert.Single((await scenario.ListAsync(tenant, 10, null)).Items);
        Assert.Equal(progress, await scenario.Store.LoadCheckpointAsync(scenario.Identity));
    }

    [Theory]
    [InlineData("program", false)]
    [InlineData("program", true)]
    [InlineData("service", false)]
    [InlineData("service", true)]
    public async Task ShouldRefuseRevisionGivenStoredCompositeIdentityDiffersFromPayload(string kind, bool changeRevision)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(Schema);
        await using var source = new BoundaryProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        await using var scenario = await CreateAsync(kind, database.Database, source, tenant);
        await using (var batch = await scenario.Store.BeginAsync(new ProjectionBatchContext(scenario.Identity, ProjectionCheckpoint.Start)))
        {
            await scenario.Apply();
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var table = kind == "program" ? "program_revisions" : "client_service_revisions";
        var before = await StoredRowAsync(database, table);
        var after = changeRevision
            ? before with { Id = $"{scenario.Id}:00000000000000000002" }
            : before with { Payload = before.Payload.Replace(scenario.Id.ToString(), Uuid.CreateVersion4().ToString(), StringComparison.Ordinal) };
        await ReplaceStoredRowAsync(database, table, after);

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (scenario.Store is CassieProgramDirectory programs)
                _ = await programs.GetRevisionAsync(tenant, scenario.Id, 1);
            else if (scenario.Store is CassieClientServiceDirectory services)
                _ = await services.GetRevisionAsync(tenant, scenario.Id, 1);
        });
        var pageFailure = await Record.ExceptionAsync(async () =>
        {
            if (scenario.Store is CassieProgramDirectory programs)
                _ = await programs.ListRevisionsAsync(tenant, scenario.Id, 10, null);
            else if (scenario.Store is CassieClientServiceDirectory services)
                _ = await services.ListRevisionsAsync(tenant, scenario.Id, 10, null);
        });

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.IsType<ImmutableContentIntegrityException>(pageFailure);
        Assert.Equal(ProjectionCheckpoint.Start, await scenario.Store.LoadCheckpointAsync(scenario.Identity));
    }

    static string CurrentTable(string kind) => kind switch
    {
        "program" => "program_directory",
        "service" => "client_service_directory",
        "boundary" => "boundary_current_identities",
        "snapshot" => "scope_snapshots",
        "population" => "population_snapshots",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    static async Task<StoredRow> StoredRowAsync(CassieRepositoryTestDatabase database, string table)
    {
        await using var connection = await database.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT row_key, row_id, scope_id, sort_key, payload FROM {table}";
        await using var rows = await command.ExecuteReaderAsync();
        Assert.True(await rows.ReadAsync());
        var result = new StoredRow(rows.GetString(0), rows.GetString(1), rows.GetString(2), rows.GetString(3), rows.GetString(4));
        Assert.False(await rows.ReadAsync());
        return result;
    }

    static async Task ReplaceStoredRowAsync(CassieRepositoryTestDatabase database, string table, StoredRow row)
    {
        await using var connection = await database.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE {table} SET row_id = @id, scope_id = @scope, sort_key = @sort, payload = @payload WHERE row_key = @key";
        foreach (var (name, value) in new[] { ("id", row.Id), ("scope", row.Scope), ("sort", row.Sort), ("payload", row.Payload), ("key", row.Key) })
            _ = command.Parameters.Add(new CassieParameter { ParameterName = name, Value = value });
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    sealed record StoredRow(string Key, string Id, string Scope, string Sort, string Payload);

    static async Task<Scenario> CreateAsync(string kind, CassieProjectionDatabase database, BoundaryProjectionScenario source, Uuid tenant,
        Uuid? selectedProgram = null, DateTimeOffset? frozenAt = null)
    {
        var id = Uuid.CreateVersion4();
        var program = selectedProgram ?? Uuid.CreateVersion4();
        var at = frozenAt ?? Now;
        var actor = Uuid.CreateVersion4();
        switch (kind)
        {
            case "program":
                var created = await ProgramSeeds.CreatedAsync(source.Content, tenant, id, "Readiness", new ProgramPlan(null, null, null, null, null, null), actor, "Author", at);
                var programs = new CassieProgramDirectory(database, source.Content);
                return new(programs, Identity("ProgramDirectory", tenant), async () => await programs.ApplyAsync(created),
                    async scope => await programs.GetAsync(scope, id) is not null, id, program);
            case "service":
                var service = new ProductClientServiceRules(tenant, id);
                Assert.True(service.Create(program, "Service", "Purpose", "Owner", actor, "Author", at).IsSuccess);
                var serviceFacts = await ProductClientServiceSeeds.FactsAsync(source.Content, service);
                var services = new CassieClientServiceDirectory(database, source.Content);
                return new(services, Identity("ClientServiceDirectory", tenant), async () =>
                {
                    foreach (var fact in serviceFacts)
                        await services.ApplyAsync(fact);
                }, async scope => await services.GetAsync(scope, id) is not null, id, program);
            case "boundary":
                var boundary = new BoundaryRules(tenant, id);
                Assert.True(boundary.Create(program, Uuid.CreateVersion5(id, "draft-1"), new BoundaryContent("Scope", "readiness", ["security"], []), actor, "Author", at).IsSuccess);
                var boundaryFacts = await source.PublishAsync(boundary);
                var boundaries = new CassieBoundaryDirectory(database, source.Boundaries);
                return new(boundaries, Identity(CassieBoundaryDirectory.ProjectorName, tenant), async () =>
                {
                    foreach (var fact in boundaryFacts)
                        await boundaries.ApplyAsync(fact);
                }, async scope => await boundaries.GetAsync(scope, id) is not null, id, program);
            case "snapshot":
                var manifest = new ProgramScopeManifest(1, tenant, program, 1, new string('a', 64), Uuid.CreateVersion4(), Uuid.CreateVersion4(), new string('b', 64));
                var (canonical, digest) = SnapshotContentIdentity.Manifest(manifest);
                var frozen = new SnapshotFrozen(tenant, id, id, null, program, "scope", manifest, canonical, digest, null, actor, "Author", at);
                var snapshots = new CassieSnapshotDirectory(database);
                return new(snapshots, Identity("SnapshotDirectory", tenant, "snapshots"), async () => await snapshots.ApplyAsync(frozen),
                    async scope => await snapshots.GetAsync(scope, id) is not null, id, program);
            case "population":
                var population = new PopulationSnapshotFrozen(tenant, id, id, null, "workforce", [], 0, 0, new string('d', 64), null, ActorReference.ForMember(actor, "Author"), at);
                var populations = new CassiePopulationSnapshotDirectory(database);
                return new(populations, Identity("PopulationSnapshotDirectoryV2", tenant, "population-snapshots"), async () => await populations.ApplyAsync(population),
                    async scope => (await populations.ListAsync(scope, "workforce", 10, null)).Items.Count == 1, id, program);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    static CheckpointIdentity Identity(string name, Uuid tenant, string? area = null) => new(name, EventStreamPattern.ForPattern(tenant.ToString(), area));
    sealed record Scenario(CassieProjectionRepository Store, CheckpointIdentity Identity, Func<Task> Apply, Func<Uuid, Task<bool>> Exists,
        Uuid Id, Uuid ProgramId) : IAsyncDisposable
    {
        public async Task<Page<Uuid>> ListAsync(Uuid tenant, int limit, string? cursor) => Store switch
        {
            CassieProgramDirectory programs => Map(await programs.ListAsync(tenant, limit, cursor), row => row.ProgramId),
            CassieClientServiceDirectory services => Map(await services.ListProgramAsync(tenant, ProgramId, limit, cursor), row => row.ServiceId),
            CassieBoundaryDirectory boundaries => Map(await boundaries.ListProgramAsync(tenant, ProgramId, limit, cursor), row => row.BoundaryId),
            CassieSnapshotDirectory snapshots => Map(await snapshots.ListProgramAsync(tenant, ProgramId, limit, cursor), row => row.SnapshotId),
            CassiePopulationSnapshotDirectory populations => Map(await populations.ListAsync(tenant, "workforce", limit, cursor), row => row.SnapshotId),
            _ => throw new InvalidOperationException("Unknown scope directory.")
        };
        static Page<Uuid> Map<T>(Page<T> page, Func<T, Uuid> identity) => new(page.Items.Select(identity).ToArray(), page.NextCursor);
        public ValueTask DisposeAsync() => Store.DisposeAsync();
    }
}
