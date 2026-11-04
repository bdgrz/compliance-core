using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Tests.Hosting.Persistence;

public sealed class CassieReadModelSchemaTests
{
    [Fact]
    public async Task ShouldDiscardDatabaseGivenStandardReadModelTableRemoved()
    {
        // Arrange
        var poolName = Guid.NewGuid().ToString("N");
        var broken = await CassieRepositoryTestDatabase.OpenAsync(poolName: poolName);
        var previousConnection = broken.ConnectionString;
        await CassieRepositoryTestDatabase.WithSchemaSetupAsync(async () =>
        {
            await using var connection = await broken.DataSource.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "DROP TABLE workforce_people";
            _ = await command.ExecuteNonQueryAsync();
        });

        // Act
        await broken.DisposeAsync();
        await using var current = await CassieRepositoryTestDatabase.OpenAsync(poolName: poolName);
        await using var read = await current.DataSource.OpenConnectionAsync();
        await using var query = read.CreateCommand();
        query.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_name = 'workforce_people'";
        var table = await query.ExecuteScalarAsync();

        // Assert
        Assert.NotEqual(previousConnection, current.ConnectionString);
        Assert.Equal("workforce_people", table);
    }

    [Fact]
    public async Task ShouldResetReadRowsAndCheckpointGivenDatabaseLeaseReused()
    {
        // Arrange
        var poolName = Guid.NewGuid().ToString("N");
        var previous = await CassieRepositoryTestDatabase.OpenAsync(poolName: poolName);
        var connectionString = previous.ConnectionString;
        await using (var connection = await previous.DataSource.OpenConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO workforce_people (row_key, projection_key, model_id, row_json, sort_key, correlated_user_id) VALUES ('old', 'old', 'old', '{}', 'old', '')";
            _ = await command.ExecuteNonQueryAsync();
            command.CommandText = "INSERT INTO portia_projection_checkpoints (identity, cursor, revision) VALUES ('old', 'committed', 1)";
            _ = await command.ExecuteNonQueryAsync();
        }

        // Act
        await previous.DisposeAsync();
        await using var current = await CassieRepositoryTestDatabase.OpenAsync(poolName: poolName);
        await using var read = await current.DataSource.OpenConnectionAsync();
        await using var query = read.CreateCommand();
        query.CommandText = "SELECT row_key FROM workforce_people WHERE row_key = 'old'";
        var row = await query.ExecuteScalarAsync();
        query.CommandText = "SELECT cursor FROM portia_projection_checkpoints WHERE identity = 'old'";
        var checkpoint = await query.ExecuteScalarAsync();

        // Assert
        Assert.Equal(connectionString, current.ConnectionString);
        Assert.Null(row);
        Assert.Null(checkpoint);
    }

    [Fact]
    public async Task ShouldCreateRequiredReadModelTablesGivenApplicationSchemaStartup()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync();
        string[] required = ["workforce_people", "workforce_work_relationships", "workforce_service_identities",
            "workforce_source_observations", "workforce_relationship_snapshots", "workforce_observations",
            "workforce_observation_resolutions", "methodology_template_catalog", "methodology_template_versions",
            "work_corrective_actions", "work_corrective_action_revisions", "work_finding_closures",
            "access_teams", "access_team_members", "access_roles", "access_role_permissions",
            "access_role_teams", "access_team_roles", "access_permission_states", "access_permission_grants",
            "program_directory", "program_revisions", "client_service_directory", "client_service_revisions",
            "boundary_current_identities", "boundary_version_identities", "boundary_decision_identities",
            "scope_snapshots", "population_snapshots"];
        await using var connection = await database.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'";

        // Act
        var actual = new HashSet<string>(StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                actual.Add(reader.GetString(0));
        }

        // Assert
        Assert.All(required, name => Assert.Contains(name, actual));
    }

    [Fact]
    public async Task ShouldPreserveReadModelDataGivenRepeatedSchemaStartup()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(
            ["CREATE TABLE schema_startup_probe (id TEXT PRIMARY KEY, value TEXT NOT NULL)"]);
        await using var connection = await database.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO schema_startup_probe (id, value) VALUES ('kept', 'original')";
        _ = await command.ExecuteNonQueryAsync();

        // Act
        await CassieRepositoryTestDatabase.WithSchemaSetupAsync(async () =>
        {
            await CassieReadModelSchema.InitializeAsync(database.DataSource);
            await CassieReadModelSchema.InitializeAsync(database.DataSource);
        });
        command.CommandText = "SELECT value FROM schema_startup_probe WHERE id = 'kept'";
        var value = await command.ExecuteScalarAsync();

        // Assert
        Assert.Equal("original", value);
    }

    [Fact]
    public async Task ShouldIsolateTablesGivenSeparateNativeTestDatabases()
    {
        // Arrange
        const string table = "CREATE TABLE isolated_probe (id TEXT PRIMARY KEY, value TEXT NOT NULL)";
        await using var first = await CassieRepositoryTestDatabase.OpenAsync([table]);
        await using var second = await CassieRepositoryTestDatabase.OpenAsync([table]);
        await using var firstConnection = await first.DataSource.OpenConnectionAsync();
        await using var secondConnection = await second.DataSource.OpenConnectionAsync();
        await using var write = firstConnection.CreateCommand();
        await using var read = secondConnection.CreateCommand();
        write.CommandText = "INSERT INTO isolated_probe (id, value) VALUES ('same', 'first')";
        read.CommandText = "SELECT value FROM isolated_probe WHERE id = 'same'";

        // Act
        _ = await write.ExecuteNonQueryAsync();
        var value = await read.ExecuteScalarAsync();

        // Assert
        Assert.NotEqual(first.ConnectionString, second.ConnectionString);
        Assert.Null(value);
    }

    [Fact]
    public async Task ShouldPreserveOtherDatabaseTablesGivenTestDatabaseDisposal()
    {
        // Arrange
        const string table = "CREATE TABLE disposal_probe (id TEXT PRIMARY KEY, value TEXT NOT NULL)";
        await using var retained = await CassieRepositoryTestDatabase.OpenAsync([table]);
        await using var connection = await retained.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO disposal_probe (id, value) VALUES ('kept', 'retained')";
        _ = await command.ExecuteNonQueryAsync();

        // Act
        var discarded = await CassieRepositoryTestDatabase.OpenAsync([table]);
        await discarded.DisposeAsync();
        command.CommandText = "SELECT value FROM disposal_probe WHERE id = 'kept'";
        var value = await command.ExecuteScalarAsync();

        // Assert
        Assert.Equal("retained", value);
    }

    [Fact]
    public void ShouldPreferExplicitConnectionGivenAmbientCassieConfiguration()
    {
        // Arrange
        const string configured = "Host=explicit;Database=compliance;Username=reader";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Cassie:ConnectionString"] = configured }).Build();

        // Act
        var connectionString = CassieReadModelSettings.ConnectionString(configuration);

        // Assert
        Assert.Equal(configured, connectionString);
    }
}
