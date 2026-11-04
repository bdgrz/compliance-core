using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Projections;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Readers;

sealed class CassieMethodologyTemplateDirectory(CassieProjectionDatabase database)
    : CassieProjectionRepository(database, ProjectorName), IMethodologyTemplateDirectoryReader, IMethodologyTemplateDirectoryProjection
{
    public const string ProjectorName = "MethodologyPublicationDirectoryV2";
    internal static EventStreamPattern Pattern => EventStreamPattern.ForPattern(MethodologyTemplate.Realm, MethodologyTemplate.Area);
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName, Pattern), ct);
    public ValueTask ApplyAsync(MethodologyTemplateVersionPublished fact, CancellationToken ct = default) =>
        ValueTask.FromException(new InvalidOperationException($"Publication {fact.VersionId} is not implemented on {ProjectorName}."));
    public ValueTask ApplyAsync(MethodologyTemplateVersionPublishedV4 fact, CancellationToken ct = default) =>
        ValueTask.FromException(new InvalidOperationException($"Publication {fact.Version} is not implemented on {ProjectorName}."));
    public ValueTask<MethodologyTemplatePublishedIdentity?> FindVersionAsync(Uuid versionId, CancellationToken ct = default) =>
        ValueTask.FromResult<MethodologyTemplatePublishedIdentity?>(versionId == Uuid.Empty ? null : null);
    public ValueTask<Page<MethodologyTemplateCatalogIdentity>> ListAsync(int limit, string? cursor, CancellationToken ct = default) =>
        ValueTask.FromResult(new Page<MethodologyTemplateCatalogIdentity>([], cursor));
}
