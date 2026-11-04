using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cntryl.Cassie.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Readers;

/// <summary>Native program/scope discovery rows and business query ordering within Portia's active projection scope.</summary>
abstract class CassieProgramScopeRepository : CassieProjectionRepository
{
    readonly string _componentName;
    readonly string? _area;

    protected CassieProgramScopeRepository(CassieProjectionDatabase database, string componentName, string? area = null)
        : base(database, componentName)
    {
        _componentName = componentName;
        _area = area;
    }
    protected string LiveKey(Uuid tenantId) => GetProjectionKey(new CheckpointIdentity(_componentName,
        EventStreamPattern.ForPattern(tenantId.ToString(), _area)));

    protected async ValueTask<T?> CurrentAsync<T>(string table, string id, JsonTypeInfo<T> json, CancellationToken ct)
    {
        await using var command = CreateCommand($"SELECT payload FROM {table} WHERE row_key = @key AND projection_key = @scope");
        Bind(command, ("key", RowKey(CurrentProjectionKey, id)), ("scope", CurrentProjectionKey));
        return await ValueAsync(command, json, ct).ConfigureAwait(false);
    }

    protected async ValueTask<T?> ReadAsync<T>(string table, Uuid tenantId, string id, JsonTypeInfo<T> json, CancellationToken ct)
    {
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand($"SELECT payload FROM {table} WHERE row_key = @key AND projection_key = @scope");
        Bind(command, ("key", RowKey(LiveKey(tenantId), id)), ("scope", LiveKey(tenantId)));
        return await ValueAsync(command, json, ct).ConfigureAwait(false);
    }

    protected async ValueTask InsertAsync<T>(string table, Uuid tenantId, string id, string scopeId, string sort,
        T value, JsonTypeInfo<T> json, CancellationToken ct)
    {
        EnsureTenant(tenantId);
        await using var command = CreateCommand($"INSERT INTO {table} (row_key, projection_key, row_id, scope_id, sort_key, payload) VALUES (@key, @scope, @id, @group, @sort, @payload)");
        Bind(command, ("key", RowKey(CurrentProjectionKey, id)), ("scope", CurrentProjectionKey), ("id", id), ("group", scopeId), ("sort", sort), ("payload", JsonSerializer.Serialize(value, json)));
        _ = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    protected async ValueTask ReplaceAsync<T>(string table, Uuid tenantId, string id, string scopeId, string sort,
        T value, JsonTypeInfo<T> json, CancellationToken ct)
    {
        EnsureTenant(tenantId);
        await using var command = CreateCommand($"UPDATE {table} SET scope_id = @group, sort_key = @sort, payload = @payload WHERE row_key = @key AND projection_key = @scope");
        Bind(command, ("key", RowKey(CurrentProjectionKey, id)), ("scope", CurrentProjectionKey), ("group", scopeId), ("sort", sort), ("payload", JsonSerializer.Serialize(value, json)));
        if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 1)
            throw new InvalidOperationException("A program/scope projection update requires its preceding row.");
    }

    protected async ValueTask DeleteAsync(string table, Uuid tenantId, string id, CancellationToken ct)
    {
        EnsureTenant(tenantId);
        await using var command = CreateCommand($"DELETE FROM {table} WHERE row_key = @key AND projection_key = @scope");
        Bind(command, ("key", RowKey(CurrentProjectionKey, id)), ("scope", CurrentProjectionKey));
        _ = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    protected async ValueTask<Page<T>> PageAsync<T>(string table, Uuid tenantId, string? scopeId, int limit,
        string? cursor, JsonTypeInfo<T> json, CancellationToken ct)
    {
        var key = LiveKey(tenantId);
        var query = key + table.Length.ToString(CultureInfo.InvariantCulture) + ":" + table +
            (scopeId is null ? "n;" : scopeId.Length.ToString(CultureInfo.InvariantCulture) + ":" + scopeId);
        var after = Decode(cursor, query);
        var take = Math.Clamp(limit, 1, 200);
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand($"SELECT payload, sort_key, row_id FROM {table} WHERE projection_key = @scope" +
            (scopeId is null ? "" : " AND scope_id = @group") +
            (after is null ? "" : " AND (sort_key > @sort OR (sort_key = @sort AND row_id > @id))") +
            " ORDER BY sort_key, row_id LIMIT " + (take + 1).ToString(CultureInfo.InvariantCulture));
        Bind(command, ("scope", key));
        if (scopeId is not null)
            Bind(command, ("group", scopeId));
        if (after is { } position)
            Bind(command, ("sort", position.Sort), ("id", position.Id));
        var items = new List<T>();
        string? lastSort = null;
        string? lastId = null;
        var more = false;
        await using var rows = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rows.ReadAsync(ct).ConfigureAwait(false))
        {
            if (items.Count == take)
            {
                more = true;
                break;
            }
            items.Add(Deserialize(rows.GetString(0), json));
            lastSort = rows.GetString(1);
            lastId = rows.GetString(2);
        }
        return new(items, more ? Encode(query, lastSort!, lastId!) : null);
    }

    void EnsureTenant(Uuid tenantId)
    {
        if (tenantId == Uuid.Empty || Identity.Pattern != EventStreamPattern.ForPattern(tenantId.ToString(), _area))
            throw new InvalidOperationException("A program/scope projection fact must belong to its exact active tenant and source pattern.");
    }
    static async ValueTask<T?> ValueAsync<T>(CassieCommand command, JsonTypeInfo<T> json, CancellationToken ct)
    {
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is null ? default : Deserialize((string)value, json);
    }
    static T Deserialize<T>(string value, JsonTypeInfo<T> json) => JsonSerializer.Deserialize(value, json)
        ?? throw new InvalidOperationException("The program/scope projection row is invalid.");
    static void Bind(CassieCommand command, params (string Name, object Value)[] parameters)
    {
        foreach (var (name, value) in parameters)
            _ = command.Parameters.Add(new CassieParameter { ParameterName = name, Value = value });
    }
    static string RowKey(string scope, string id) => scope.Length.ToString(CultureInfo.InvariantCulture) + ":" + scope + id.Length.ToString(CultureInfo.InvariantCulture) + ":" + id;
    static string Encode(string scope, string sort, string id) => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        "cassie-program-scope.v1\n" + string.Join('\n', new[] { scope, sort, id }.Select(static value => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))))));
    static (string Sort, string Id)? Decode(string? cursor, string scope)
    {
        if (cursor is null)
            return null;
        try
        {
            if (cursor.Length > 16384)
                throw new FormatException();
            var utf8 = new UTF8Encoding(false, true);
            var parts = utf8.GetString(Convert.FromBase64String(cursor)).Split('\n');
            if (parts.Length != 4 || parts[0] != "cassie-program-scope.v1" || utf8.GetString(Convert.FromBase64String(parts[1])) != scope)
                throw new FormatException();
            return (utf8.GetString(Convert.FromBase64String(parts[2])), utf8.GetString(Convert.FromBase64String(parts[3])));
        }
        catch (Exception error) when (error is FormatException or DecoderFallbackException)
        {
            throw new ProgramScopeDirectoryQueryException();
        }
    }
}
