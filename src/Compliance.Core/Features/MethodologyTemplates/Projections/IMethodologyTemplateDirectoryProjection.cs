using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Projections;

public interface IMethodologyTemplateDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(MethodologyTemplateVersionPublished fact, CancellationToken ct = default);
}
