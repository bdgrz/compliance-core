using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Controls.Projections;

public interface IControlDraftHistoryDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
