using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments.Projections;

public interface ICommitmentDraftHistoryDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
