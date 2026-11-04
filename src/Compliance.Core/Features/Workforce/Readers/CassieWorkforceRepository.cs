using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cntryl.Cassie.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Readers;

/// <summary>Workforce row SQL; Portia owns transaction, checkpoint, and concurrency behavior.</summary>
abstract class CassieWorkforceRepository : CassieProjectionRepository
{
    readonly string _componentName;
    readonly string _pattern;

    protected CassieWorkforceRepository(CassieProjectionDatabase database, string componentName, string pattern)
        : base(database, componentName)
    {
        _componentName = componentName;
        _pattern = pattern;
    }
    protected string LiveProjectionKey(Uuid tenantId) => GetProjectionKey(
        new CheckpointIdentity(_componentName, EventStreamPattern.ForPattern(tenantId.ToString(), _pattern)));

    protected async ValueTask<T?> FindCurrentAsync<T>(string table, Uuid id, JsonTypeInfo<T> json,
        CancellationToken ct)
    {
        await using var command = CreateCommand($"SELECT row_json FROM {table} WHERE row_key = @key AND projection_key = @scope");
        Bind(command, ("key", RowKey(CurrentProjectionKey, id)), ("scope", CurrentProjectionKey));
        return await ReadOneAsync(command, json, ct).ConfigureAwait(false);
    }

    protected async ValueTask<T?> ReadRowAsync<T>(string table, Uuid tenantId, Uuid id, JsonTypeInfo<T> json,
        CancellationToken ct)
    {
        var scope = LiveProjectionKey(tenantId);
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand($"SELECT row_json FROM {table} WHERE row_key = @key AND projection_key = @scope");
        Bind(command, ("key", RowKey(scope, id)), ("scope", scope));
        return await ReadOneAsync(command, json, ct).ConfigureAwait(false);
    }

    protected async ValueTask InsertRowAsync<T>(string table, Uuid tenantId, Uuid id, T row,
        JsonTypeInfo<T> json, CancellationToken ct, params (string Name, object Value)[] columns)
    {
        EnsureTenant(tenantId);
        var names = string.Join(", ", columns.Select(static column => column.Name));
        var values = string.Join(", ", columns.Select(static column => "@" + column.Name));
        await using var command = CreateCommand($"INSERT INTO {table} (row_key, projection_key, model_id, row_json{(columns.Length == 0 ? "" : ", " + names)}) " +
            $"VALUES (@key, @scope, @id, @json{(columns.Length == 0 ? "" : ", " + values)})");
        Bind(command, ("key", RowKey(CurrentProjectionKey, id)), ("scope", CurrentProjectionKey),
            ("id", id.ToString()), ("json", JsonSerializer.Serialize(row, json)));
        Bind(command, columns);
        _ = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    protected async ValueTask ReplaceRowAsync<T>(string table, Uuid tenantId, Uuid id, T row,
        JsonTypeInfo<T> json, CancellationToken ct, params (string Name, object Value)[] columns)
    {
        EnsureTenant(tenantId);
        var assignments = string.Join(", ", columns.Select(static column => column.Name + " = @" + column.Name));
        await using var command = CreateCommand($"UPDATE {table} SET row_json = @json{(columns.Length == 0 ? "" : ", " + assignments)} " +
            "WHERE row_key = @key AND projection_key = @scope");
        Bind(command, ("key", RowKey(CurrentProjectionKey, id)), ("scope", CurrentProjectionKey),
            ("json", JsonSerializer.Serialize(row, json)));
        Bind(command, columns);
        if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 1)
            throw new InvalidOperationException("A workforce projection update requires its preceding row.");
    }

    protected async ValueTask<Page<T>> ReadPageAsync<T>(string table, Uuid tenantId, JsonTypeInfo<T> json,
        int limit, string? cursor, string query, CancellationToken ct, string sortColumn = "sort_key",
        string filter = "", params (string Name, object Value)[] parameters)
    {
        var scope = LiveProjectionKey(tenantId);
        var cursorScope = WorkforceDirectoryCursor.Scope(scope, table, query);
        var after = WorkforceDirectoryCursor.Decode(cursor, cursorScope);
        var take = Math.Clamp(limit, 1, 200);
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand($"SELECT row_json, {sortColumn}, model_id FROM {table} WHERE projection_key = @scope{filter}" +
            (after is null ? "" : $" AND ({sortColumn} > @after OR ({sortColumn} = @after AND model_id > @after_id))") +
            $" ORDER BY {sortColumn}, model_id LIMIT @take");
        Bind(command, ("scope", scope), ("take", take + 1));
        Bind(command, parameters);
        if (after is { } position)
            Bind(command, ("after", position.Sort), ("after_id", position.Id));
        var rows = new List<T>();
        string? lastSort = null;
        string? lastId = null;
        var more = false;
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (rows.Count == take)
            {
                more = true;
                break;
            }
            rows.Add(Deserialize(reader.GetString(0), json));
            lastSort = reader.GetString(1);
            lastId = reader.GetString(2);
        }
        return new Page<T>(rows, more ? WorkforceDirectoryCursor.Encode(cursorScope, lastSort!, lastId!) : null);
    }

    protected static void Bind(CassieCommand command, params (string Name, object Value)[] parameters)
    {
        foreach (var parameter in parameters)
            _ = command.Parameters.Add(new CassieParameter { ParameterName = parameter.Name, Value = parameter.Value });
    }

    protected static T Deserialize<T>(string value, JsonTypeInfo<T> json) =>
        JsonSerializer.Deserialize(value, json) ?? throw new InvalidOperationException("The workforce projection row is invalid.");

    static async ValueTask<T?> ReadOneAsync<T>(CassieCommand command, JsonTypeInfo<T> json, CancellationToken ct)
    {
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is null ? default : Deserialize((string)value, json);
    }

    void EnsureTenant(Uuid tenantId)
    {
        if (Identity.Pattern.Realm != tenantId.ToString())
            throw new InvalidOperationException("A workforce projection fact must belong to its active tenant.");
    }

    protected static string RowKey(string scope, Uuid id) => scope + "|" + id;
}
