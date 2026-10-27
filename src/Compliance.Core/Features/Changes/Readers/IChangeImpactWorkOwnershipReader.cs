using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes.Readers;

public interface IChangeImpactWorkOwnershipReader
{
    ValueTask<Result> ConfirmOwnedByProgramAsync(Uuid tenantId, Uuid programId,
        IReadOnlyCollection<Uuid> workItemIds, CancellationToken ct = default);
}
