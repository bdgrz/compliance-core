using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Projections;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Projectors;

public sealed partial class MethodologyTemplateDirectoryProjector(IMethodologyTemplateDirectoryProjection projection)
    : Projector(projection, FitzMethodologyTemplateDirectory.Pattern, FitzMethodologyTemplateDirectory.ProjectorName),
        IProjectorHandler<MethodologyTemplateVersionPublished>
{
    public ValueTask HandleAsync(MethodologyTemplateVersionPublished fact, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(fact, ct);
}
