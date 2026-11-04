using System.Collections.Concurrent;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Cassie.Connection;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Exclusive native database leases; Portia retains all projection transaction behavior.</summary>
sealed class CassieRepositoryTestDatabase : IAsyncDisposable
{
    static readonly ConcurrentDictionary<(string ConnectionString, string Name), ConcurrentStack<AvailableDatabase>> Pools = new();
    static readonly SemaphoreSlim SchemaSetup = new(1, 1);
    readonly ConcurrentStack<AvailableDatabase> _pool;
    readonly AvailableDatabase _available;
    bool _disposed;

    CassieRepositoryTestDatabase(AvailableDatabase available, ConcurrentStack<AvailableDatabase> pool)
    {
        _available = available;
        _pool = pool;
        ConnectionString = available.ConnectionString;
        DataSource = new CassieDataSource(ConnectionString);
        Database = new CassieProjectionDatabase(DataSource);
    }

    public string ConnectionString { get; }
    public CassieDataSource DataSource { get; }
    public CassieProjectionDatabase Database { get; }

    public static async Task WithSchemaSetupAsync(Func<Task> setup, CancellationToken ct = default)
    {
        await SchemaSetup.WaitAsync(ct);
        try
        {
            await setup();
        }
        finally
        {
            SchemaSetup.Release();
        }
    }

    public static async ValueTask<CassieRepositoryTestDatabase> OpenAsync(
        IEnumerable<string>? additionalStatements = null, string? poolName = null, CancellationToken ct = default)
    {
        await SchemaSetup.WaitAsync(ct);
        try
        {
            return await OpenCoreAsync(additionalStatements, poolName, ct);
        }
        finally
        {
            SchemaSetup.Release();
        }
    }

    static async ValueTask<CassieRepositoryTestDatabase> OpenCoreAsync(
        IEnumerable<string>? additionalStatements, string? poolName, CancellationToken ct)
    {
        var configured = Environment.GetEnvironmentVariable("COMPLIANCE_CASSIE_TEST_CONNECTION_STRING")
            ?? "Host=127.0.0.1;Port=55432;Database=postgres;Username=root;Password=compliance-cassie-test;SSL Mode=Disable;Timeout=15";
        var pool = Pools.GetOrAdd((configured, poolName ?? "default"), static _ => new ConcurrentStack<AvailableDatabase>());
        if (!pool.TryPop(out var available))
        {
            await using var administrator = new CassieDataSource(configured);
            await using var connection = await administrator.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            var databaseName = "compliance_test_" + Guid.NewGuid().ToString("N");
            command.CommandText = $"CREATE DATABASE {databaseName}";
            _ = await command.ExecuteNonQueryAsync(ct);
            var builder = new CassieConnectionStringBuilder(configured) { Database = databaseName };
            available = new AvailableDatabase(builder.ConnectionString);
        }
        var database = new CassieRepositoryTestDatabase(available, pool);
        try
        {
            await database.Database.InitializeAsync(ct);
            if (available.StandardTables is null)
            {
                await CassieReadModelSchema.InitializeAsync(database.DataSource, ct);
                available.StandardTables = (await database.ReadTablesAsync(ct))
                    .Select(static table => Qualified(table.Schema, table.Table)).ToHashSet(StringComparer.Ordinal);
            }
            var additional = additionalStatements?.Where(static statement =>
                !CassieReadModelSchema.Statements.Contains(statement, StringComparer.Ordinal)).ToArray() ?? [];
            if (additional.Length > 0)
            {
                await using var application = await database.DataSource.OpenConnectionAsync(ct);
                await using var schema = application.CreateCommand();
                foreach (var statement in additional)
                {
                    schema.CommandText = statement;
                    _ = await schema.ExecuteNonQueryAsync(ct);
                }
            }
            return database;
        }
        catch (Exception original)
        {
            try
            {
                await database.DisposeCoreAsync(reuse: false);
            }
            catch (Exception cleanup)
            {
                original.Data["Compliance.CassieTestCleanupException"] = cleanup;
            }
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await SchemaSetup.WaitAsync();
        try
        {
            await DisposeCoreAsync(reuse: true);
        }
        finally
        {
            SchemaSetup.Release();
        }
    }

    async ValueTask DisposeCoreAsync(bool reuse)
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            reuse = await ResetTablesAsync(reuse);
        }
        finally
        {
            Database.Dispose();
            await DataSource.DisposeAsync();
        }
        if (reuse)
            _pool.Push(_available);
        // Failed leases are never reused. The dedicated memory-backed test service
        // reclaims remaining metadata; the pinned engine cannot drop emptied DBs (#1183).
    }

    async Task<bool> ResetTablesAsync(bool reuse)
    {
        if (!reuse)
            return false;
        var tables = await ReadTablesAsync();
        if (_available.StandardTables?.IsSubsetOf(
                tables.Select(static table => Qualified(table.Schema, table.Table))) != true)
            return false;
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        foreach (var (schema, table) in tables)
        {
            var qualified = Qualified(schema, table);
            var keepSchema = _available.StandardTables.Contains(qualified);
            command.CommandText = (keepSchema ? "DELETE FROM " : "DROP TABLE ") + qualified;
            _ = await command.ExecuteNonQueryAsync();
        }
        return true;
    }

    async Task<IReadOnlyList<(string Schema, string Table)>> ReadTablesAsync(CancellationToken ct = default)
    {
        await using var connection = await DataSource.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        // Cassie's table discovery is scoped to this connection's database.
        command.CommandText = "SELECT table_schema, table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE'";
        var tables = new List<(string Schema, string Table)>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            tables.Add((reader.GetString(0), reader.GetString(1)));
        return tables;
    }

    static string Qualified(string schema, string table) => Quote(schema) + "." + Quote(table);
    static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    sealed class AvailableDatabase(string connectionString)
    {
        public string ConnectionString { get; } = connectionString;
        public HashSet<string>? StandardTables { get; set; }
    }
}
