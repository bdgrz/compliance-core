using System.Data.Common;
using System.Globalization;
using Cntryl.Cassie.Commands;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Projections;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Readers;

/// <summary>Native Cassie discovery identities; authoritative content remains verified against exact published sources.</summary>
sealed class CassieMethodologyTemplateDirectory(CassieProjectionDatabase database)
    : CassieProjectionRepository(database, ProjectorName), IMethodologyTemplateDirectoryReader, IMethodologyTemplateDirectoryProjection
{
    public const string ProjectorName = "MethodologyPublicationDirectoryV2";
    internal static EventStreamPattern Pattern => EventStreamPattern.ForPattern(MethodologyTemplate.Realm, MethodologyTemplate.Area);
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName, Pattern), ct);

    public ValueTask ApplyAsync(MethodologyTemplateVersionPublished fact, CancellationToken ct = default)
    {
        if (fact.SubjectId == Uuid.Empty || fact.TenantId != Uuid.Empty || fact.VersionId == Uuid.Empty ||
            fact.VersionNumber < 1 || fact.VersionStatus != "published" || fact.OccurredAt == default ||
            fact.Operation.Owner != new MethodologyTemplate(fact.SubjectId).Stream ||
            fact.Operation.CommandKind != nameof(ApproveMethodologyTemplateVersion))
            throw new InvalidOperationException("A publication identity must come from its exact native platform source.");
        return ApplyIdentityAsync(new MethodologyTemplatePublishedIdentity(fact.VersionId, fact.SubjectId,
            fact.VersionNumber, fact.OccurredAt), ct);
    }

    public ValueTask ApplyAsync(MethodologyTemplateVersionPublishedV4 fact, CancellationToken ct = default)
    {
        if (fact.TemplateId == Uuid.Empty || fact.Version < 1 || fact.DraftId == Uuid.Empty || fact.DraftRevision < 0 ||
            fact.ApprovalEventId == Uuid.Empty || fact.ApprovedBy == Uuid.Empty || fact.Metadata.IsAudit ||
            fact.Metadata.AggregateId != fact.TemplateId || fact.Metadata.OccurredOn == default ||
            !MethodologyTemplateDraft.ValidReference(fact.EntriesContent))
            throw new InvalidOperationException("A separate-draft publication identity requires its exact native platform source.");
        return ApplyIdentityAsync(new MethodologyTemplatePublishedIdentity(
            MethodologyTemplate.PublishedVersionIdentity(fact.TemplateId, fact.Version), fact.TemplateId, fact.Version,
            fact.Metadata.OccurredOn, SeparateDraftLine: true), ct);
    }

    async ValueTask ApplyIdentityAsync(MethodologyTemplatePublishedIdentity version, CancellationToken ct)
    {
        var key = CurrentProjectionKey;
        await using (var insert = CreateCommand($"""
            INSERT INTO {CassieMethodologyTemplateSchema.Versions}
                (row_key, version_key, projection_key, version_id, template_id, version_number, published_ticks, separate_draft_line)
            VALUES (@row, @unique, @scope, @version, @template, @number, @ticks, @separate)
            ON CONFLICT (row_key) DO NOTHING
            """))
        {
            Bind(insert, ("row", RowKey(key, version.VersionId.ToString())),
                ("unique", RowKey(key, $"{version.TemplateId}:{version.VersionNumber.ToString(CultureInfo.InvariantCulture)}")),
                ("scope", key), ("version", version.VersionId.ToString()), ("template", version.TemplateId.ToString()),
                ("number", version.VersionNumber), ("ticks", version.PublishedAt.UtcTicks), ("separate", version.SeparateDraftLine));
            _ = await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        await using (var check = CreateCommand($"""
            SELECT template_id, version_number, published_ticks, separate_draft_line
            FROM {CassieMethodologyTemplateSchema.Versions} WHERE projection_key = @scope AND version_id = @version
            """))
        {
            Bind(check, ("scope", key), ("version", version.VersionId.ToString()));
            await using var row = await check.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await row.ReadAsync(ct).ConfigureAwait(false) || ReadVersion(row, version.VersionId) != version)
                throw new InvalidOperationException("A published version identity cannot change its template or immutable source.");
        }
        await using (var insert = CreateCommand($"""
            INSERT INTO {CassieMethodologyTemplateSchema.Catalog}
                (row_key, projection_key, template_id, first_published_ticks, separate_draft_line)
            VALUES (@row, @scope, @template, @ticks, @separate)
            ON CONFLICT (row_key) DO NOTHING
            """))
        {
            Bind(insert, ("row", RowKey(key, version.TemplateId.ToString())), ("scope", key), ("template", version.TemplateId.ToString()),
                ("ticks", version.PublishedAt.UtcTicks), ("separate", version.SeparateDraftLine));
            _ = await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        await using var catalog = CreateCommand($"""
            SELECT separate_draft_line FROM {CassieMethodologyTemplateSchema.Catalog}
            WHERE projection_key = @scope AND template_id = @template
            """);
        Bind(catalog, ("scope", key), ("template", version.TemplateId.ToString()));
        var family = await catalog.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (family is not bool separate || separate != version.SeparateDraftLine)
            throw new InvalidOperationException("A template identity cannot mix legacy and separate-draft publication sources.");
    }

    public async ValueTask<MethodologyTemplatePublishedIdentity?> FindVersionAsync(Uuid versionId, CancellationToken ct = default)
    {
        if (versionId == Uuid.Empty)
            return null;
        var key = GetProjectionKey(new CheckpointIdentity(ProjectorName, Pattern));
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        await using var command = read.CreateCommand($"""
            SELECT template_id, version_number, published_ticks, separate_draft_line
            FROM {CassieMethodologyTemplateSchema.Versions} WHERE projection_key = @scope AND version_id = @version
            """);
        Bind(command, ("scope", key), ("version", versionId.ToString()));
        await using var row = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await row.ReadAsync(ct).ConfigureAwait(false) ? ReadVersion(row, versionId) : null;
    }

    public async ValueTask<Page<MethodologyTemplateCatalogIdentity>> ListAsync(int limit, string? cursor, CancellationToken ct = default)
    {
        var take = Math.Clamp(limit, 1, 200);
        var key = GetProjectionKey(new CheckpointIdentity(ProjectorName, Pattern));
        long ticks = 0;
        var template = Uuid.Empty;
        if (cursor is not null && !MethodologyTemplateDirectoryCursor.TryDecode(key, cursor, out ticks, out template))
            throw new MethodologyTemplateDirectoryQueryException("The methodology directory cursor is invalid for this projection.");
        await using var read = await OpenReadAsync(ct).ConfigureAwait(false);
        var after = cursor is null ? "" : "AND (first_published_ticks > @ticks OR (first_published_ticks = @ticks AND template_id > @template))";
        await using var command = read.CreateCommand($"""
            SELECT template_id, first_published_ticks, separate_draft_line FROM {CassieMethodologyTemplateSchema.Catalog}
            WHERE projection_key = @scope {after} ORDER BY first_published_ticks, template_id LIMIT {(take + 1).ToString(CultureInfo.InvariantCulture)}
            """);
        Bind(command, ("scope", key));
        if (cursor is not null)
            Bind(command, ("ticks", ticks), ("template", template.ToString()));
        var rows = new List<MethodologyTemplateCatalogIdentity>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            rows.Add(new MethodologyTemplateCatalogIdentity(Uuid.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
                new DateTimeOffset(reader.GetInt64(1), TimeSpan.Zero), reader.GetBoolean(2)));
        var items = rows.Take(take).ToArray();
        var next = rows.Count > take ? MethodologyTemplateDirectoryCursor.Encode(key, items[^1].FirstPublishedAt.UtcTicks,
            items[^1].TemplateId) : null;
        return new Page<MethodologyTemplateCatalogIdentity>(items, next);
    }

    static string RowKey(string projectionKey, string identity) =>
        $"{projectionKey.Length.ToString(CultureInfo.InvariantCulture)}:{projectionKey}{identity.Length.ToString(CultureInfo.InvariantCulture)}:{identity}";

    static MethodologyTemplatePublishedIdentity ReadVersion(DbDataReader row, Uuid versionId) => new(versionId,
        Uuid.Parse(row.GetString(0), CultureInfo.InvariantCulture), row.GetInt64(1),
        new DateTimeOffset(row.GetInt64(2), TimeSpan.Zero), row.GetBoolean(3));
    static void Bind(DbCommand command, params (string Name, object Value)[] values)
    {
        foreach (var (name, value) in values)
            _ = command.Parameters.Add(new CassieParameter { ParameterName = name, Value = value });
    }
}
