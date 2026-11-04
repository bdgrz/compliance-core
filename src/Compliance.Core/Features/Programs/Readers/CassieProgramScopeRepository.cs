using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cntryl.Cassie.Commands;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Snapshots.Models;

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
        await using var command = CreateCommand($"SELECT payload, row_key, row_id, scope_id, sort_key FROM {table} WHERE row_key = @key AND projection_key = @scope");
        Bind(command, ("key", RowKey(CurrentProjectionKey, id)), ("scope", CurrentProjectionKey));
        return await ValueAsync(command, table, CurrentProjectionKey, Uuid.Parse(Identity.Pattern.Realm, CultureInfo.InvariantCulture), id, json, ct).ConfigureAwait(false);
    }

    protected async ValueTask<T?> ReadAsync<T>(string table, Uuid tenantId, string id, JsonTypeInfo<T> json, CancellationToken ct)
    {
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand($"SELECT payload, row_key, row_id, scope_id, sort_key FROM {table} WHERE row_key = @key AND projection_key = @scope");
        Bind(command, ("key", RowKey(LiveKey(tenantId), id)), ("scope", LiveKey(tenantId)));
        return await ValueAsync(command, table, LiveKey(tenantId), tenantId, id, json, ct).ConfigureAwait(false);
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
        await using var command = read.CreateCommand($"SELECT payload, row_key, row_id, scope_id, sort_key FROM {table} WHERE projection_key = @scope" +
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
            items.Add(ValidateRow(table, key, tenantId, null, rows.GetString(0), rows.GetString(1),
                rows.GetString(2), rows.GetString(3), rows.GetString(4), json));
            lastSort = rows.GetString(4);
            lastId = rows.GetString(2);
        }
        return new(items, more ? Encode(query, lastSort!, lastId!) : null);
    }

    void EnsureTenant(Uuid tenantId)
    {
        if (tenantId == Uuid.Empty || Identity.Pattern != EventStreamPattern.ForPattern(tenantId.ToString(), _area))
            throw new InvalidOperationException("A program/scope projection fact must belong to its exact active tenant and source pattern.");
    }
    static async ValueTask<T?> ValueAsync<T>(CassieCommand command, string table, string projectionKey,
        Uuid tenantId, string id, JsonTypeInfo<T> json, CancellationToken ct)
    {
        await using var rows = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await rows.ReadAsync(ct).ConfigureAwait(false)
            ? ValidateRow(table, projectionKey, tenantId, id, rows.GetString(0), rows.GetString(1),
                rows.GetString(2), rows.GetString(3), rows.GetString(4), json)
            : default;
    }

    static T ValidateRow<T>(string table, string projectionKey, Uuid tenantId, string? requestedId,
        string payload, string key, string id, string scope, string sort, JsonTypeInfo<T> json)
    {
        T value;
        try
        {
            value = JsonSerializer.Deserialize(payload, json)
                ?? throw new ImmutableContentIntegrityException("The program/scope projection payload is missing.");
        }
        catch (JsonException error)
        {
            throw new ImmutableContentIntegrityException("The program/scope projection payload is invalid: " + error.Message);
        }
        StoredIdentity expected = (table, value) switch
        {
            ("program_directory", ProgramView row) when row.Name is not null =>
                new(row.TenantId, row.ProgramId.ToString(), "", row.Name.ToUpperInvariant()),
            ("program_revisions", ProgramRevisionView row) when row.Revision > 0 =>
                new(null, RevisionKey(row.ProgramId, row.Revision), row.ProgramId.ToString(), Number(row.Revision)),
            ("client_service_directory", ClientServiceView row) when row.Name is not null =>
                new(row.TenantId, row.ServiceId.ToString(), (row.ProgramId ?? Uuid.Empty).ToString(), row.Name.ToUpperInvariant()),
            ("client_service_revisions", ClientServiceRevisionView row) when row.Revision > 0 =>
                new(null, RevisionKey(row.ServiceId, row.Revision), row.ServiceId.ToString(), Number(row.Revision)),
            ("boundary_current_identities", BoundaryDirectoryIdentity row) =>
                new(row.Header.TenantId, row.Header.BoundaryId.ToString(), row.Header.ProgramId.ToString(), row.Header.BoundaryId.ToString()),
            ("boundary_version_identities", BoundaryDirectoryIdentity row) when row.VersionId is { } version && row.EffectiveFrom is { } effective =>
                new(row.Header.TenantId, $"{row.Header.BoundaryId}:{version}", row.Header.BoundaryId.ToString(), effective.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("boundary_decision_identities", BoundaryDirectoryIdentity row) when row.DecisionId is { } decision && row.DecidedAt is { } decided =>
                new(row.Header.TenantId, $"{row.Header.BoundaryId}:{decision}", row.Header.BoundaryId.ToString(), Number(decided.UtcTicks)),
            ("scope_snapshots", SnapshotView row) =>
                new(row.TenantId, row.SnapshotId.ToString(), row.ProgramId.ToString(), Number(row.FrozenAt.UtcTicks)),
            ("population_snapshots", PopulationSnapshotSummary row) =>
                new(row.TenantId, row.SnapshotId.ToString(), row.Kind, Number(long.MaxValue - row.FrozenAt.UtcTicks)),
            _ => throw new ImmutableContentIntegrityException("The program/scope projection row has an invalid identity contract."),
        };
        if (expected.Tenant is { } tenant && tenant != tenantId ||
            requestedId is not null && requestedId != expected.Id ||
            key != RowKey(projectionKey, expected.Id) || id != expected.Id || scope != expected.Scope || sort != expected.Sort)
            throw new ImmutableContentIntegrityException("The program/scope projection row differs from its exact stored tenant, identity, scope or ordering.");
        return value;
    }

    static string Number(long value) => value.ToString("D20", CultureInfo.InvariantCulture);
    static string RevisionKey(Uuid id, long revision) => $"{id}:{Number(revision)}";
    readonly record struct StoredIdentity(Uuid? Tenant, string Id, string Scope, string Sort);

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
