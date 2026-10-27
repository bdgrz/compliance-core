using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls.Projections;

public interface IControlDraftDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
