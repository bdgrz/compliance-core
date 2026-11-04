using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Projections;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Projectors;

public sealed partial class MethodologyTemplateDirectoryProjector(IMethodologyTemplateDirectoryProjection projection)
    : Projector(projection, FitzMethodologyTemplateDirectory.Pattern, FitzMethodologyTemplateDirectory.ProjectorName),
        IProjectorHandler<MethodologyTemplateVersionPublished>, IProjectorHandler<MethodologyTemplateVersionPublishedV4>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<MethodologyTemplateVersionPublished> context,
        CancellationToken ct)
    {
        var fact = context.Trigger;
        await projection.ApplyAsync(fact, ct).ConfigureAwait(false);
        return Result.Success;
    }
    public async ValueTask<Result> HandleAsync(IProjectorContext<MethodologyTemplateVersionPublishedV4> context, CancellationToken ct)
    {
        await projection.ApplyAsync(context.Trigger, ct).ConfigureAwait(false);
        return Result.Success;
    }
}

