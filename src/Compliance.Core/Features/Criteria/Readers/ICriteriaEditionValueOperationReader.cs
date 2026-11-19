using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Readers;

/// <summary>Reads one exact source-verified target-scoped edition value receipt.</summary>
public interface ICriteriaEditionValueOperationReader
{
    /// <summary>Returns pending unless the exact claim and terminal outcome are source-qualified.</summary>
    ValueTask<Result<CriteriaOperationReceipt>> ReadAsync(Uuid editionId, string targetKind, string? field,
        string? category, string? code, Uuid operationId, CancellationToken ct = default);
}
