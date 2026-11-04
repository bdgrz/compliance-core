using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Readers;

public sealed class MethodologyTemplateLineReader
{
    public MethodologyTemplateLineReader(IAuthoritativeSnapshotReader snapshots, IImmutableContentStore content)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(content);
    }
    public ValueTask<MethodologyTemplateLineView?> ReadAsync(Uuid templateId, CancellationToken ct = default) =>
        ValueTask.FromResult<MethodologyTemplateLineView?>(templateId == Uuid.Empty ? null : null);
    public ValueTask<MethodologyTemplatePublishedVersionView?> VersionAsync(Uuid templateId, long version,
        CancellationToken ct = default) => ValueTask.FromResult<MethodologyTemplatePublishedVersionView?>(templateId == Uuid.Empty ? null : null);
    public ValueTask<MethodologyTemplatePublicationView?> PublicationAsync(Uuid templateId, CancellationToken ct = default) =>
        ValueTask.FromResult<MethodologyTemplatePublicationView?>(templateId == Uuid.Empty ? null : null);
}
