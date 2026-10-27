using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Projections;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Readers;

sealed class FitzMethodologyTemplateDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/methodology-publication-directory/projection-v1", ProjectorName),
        IMethodologyTemplateDirectoryReader, IMethodologyTemplateDirectoryProjection
{
    public const string ProjectorName = "MethodologyPublicationDirectoryV1";
    internal static EventStreamPattern Pattern => EventStreamPattern.ForPattern(MethodologyTemplate.Realm,
        MethodologyTemplate.Area);
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName, Pattern), ct);

    public async ValueTask ApplyAsync(MethodologyTemplateVersionPublished fact, CancellationToken ct = default)
    {
        if (fact.SubjectId == Uuid.Empty || fact.TenantId != Uuid.Empty || fact.VersionId == Uuid.Empty ||
            fact.VersionNumber < 1 || fact.VersionStatus != "published" || fact.OccurredAt == default ||
            fact.Operation.Owner != new MethodologyTemplate(fact.SubjectId).Stream ||
            fact.Operation.CommandKind != nameof(ApproveMethodologyTemplateVersion))
            throw new InvalidOperationException("A publication identity must come from its exact native platform source.");
        var version = new MethodologyTemplatePublishedIdentity(fact.VersionId, fact.SubjectId, fact.VersionNumber,
            fact.OccurredAt);
        var existing = await MethodologyTemplateDirectorySchema.Versions.GetAsync(Transaction, fact.VersionId, ct)
            .ConfigureAwait(false);
        if (existing is not null && existing != version)
            throw new InvalidOperationException("A published version identity cannot change its template or immutable source.");
        if (existing is null)
            await MethodologyTemplateDirectorySchema.Versions.InsertAsync(Transaction, version, ct).ConfigureAwait(false);
        if (await MethodologyTemplateDirectorySchema.Templates.GetAsync(Transaction, fact.SubjectId, ct)
                .ConfigureAwait(false) is null)
            await MethodologyTemplateDirectorySchema.Templates.InsertAsync(Transaction,
                new MethodologyTemplateCatalogIdentity(fact.SubjectId, fact.OccurredAt), ct).ConfigureAwait(false);
    }

    public async ValueTask<MethodologyTemplatePublishedIdentity?> FindVersionAsync(Uuid versionId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(MethodologyTemplate.Realm, ct).ConfigureAwait(false);
        return await MethodologyTemplateDirectorySchema.Versions.GetAsync(tx, versionId, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<MethodologyTemplateCatalogIdentity>> ListAsync(int limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(MethodologyTemplate.Realm, ct).ConfigureAwait(false);
        return await MethodologyTemplateDirectorySchema.Templates.QueryAsync(tx,
            MethodologyTemplateDirectorySchema.ByFirstPublished.Query().Take(Math.Clamp(limit, 1, 200)).After(cursor), ct)
            .ConfigureAwait(false);
    }
}
