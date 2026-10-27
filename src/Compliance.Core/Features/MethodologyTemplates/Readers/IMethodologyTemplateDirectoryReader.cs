using Bdgrz.Compliance.Features.MethodologyTemplates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Readers;

public interface IMethodologyTemplateDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(CancellationToken ct = default);
    ValueTask<MethodologyTemplatePublishedIdentity?> FindVersionAsync(Uuid versionId, CancellationToken ct = default);
    ValueTask<Page<MethodologyTemplateCatalogIdentity>> ListAsync(int limit, string? cursor,
        CancellationToken ct = default);
}
