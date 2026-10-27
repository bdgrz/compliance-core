using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Projections;

public interface IMethodologyTemplateDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(MethodologyTemplateVersionPublished fact, CancellationToken ct = default);
}
