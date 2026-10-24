using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public interface IMethodologyTemplateDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(MethodologyTemplateVersionPublished fact, CancellationToken ct = default);
}
