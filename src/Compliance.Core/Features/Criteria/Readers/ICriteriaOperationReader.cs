using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Readers;

/// <summary>Reads draft and operation state from its authoritative source.</summary>
public interface ICriteriaOperationReader
{
    ValueTask<Result<CriteriaEditionDraftView>> GetDraftAsync(Uuid editionId, CancellationToken ct = default);

    ValueTask<Result<CriteriaOperationReceipt>> GetOperationAsync(Uuid editionId, Uuid operationId,
        string? identifier, CancellationToken ct = default);
}
