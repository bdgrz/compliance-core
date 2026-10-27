using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Projections;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Projectors;

public sealed partial class MethodologyTemplateDirectoryProjector(IMethodologyTemplateDirectoryProjection projection)
    : Projector(projection, FitzMethodologyTemplateDirectory.Pattern, FitzMethodologyTemplateDirectory.ProjectorName),
        IProjectorHandler<MethodologyTemplateVersionPublished>
{
    public ValueTask HandleAsync(MethodologyTemplateVersionPublished fact, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(fact, ct);
}
