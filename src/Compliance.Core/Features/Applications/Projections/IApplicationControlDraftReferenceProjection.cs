using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Applications.Projections;

public interface IApplicationControlDraftReferenceProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
