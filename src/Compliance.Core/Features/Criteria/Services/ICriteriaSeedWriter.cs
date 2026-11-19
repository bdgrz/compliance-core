using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Trusted source-backed operations for the one fixed platform seed.</summary>
/// <remarks>
/// Implementations must authorize only an internal system process and verify the
/// exact source binding before every write. Confirmed means the exact matching
/// source fact or participant outcome is durable; successful dispatch or a
/// projection read is insufficient. Pending includes unknown commits, missing
/// outcomes and incomplete source coverage. Conflict never overwrites source.
/// </remarks>
interface ICriteriaSeedWriter
{
    /// <summary>Atomically bind the edition to one compiled release before any seed fact is written.</summary>
    ValueTask<CriteriaSeedWriteResult> BindEditionAsync(CriteriaSeedHeader header,
        Uuid operationId, CancellationToken ct);

    /// <summary>Record or verify the small edition metadata, with no entry or gap list.</summary>
    ValueTask<CriteriaSeedWriteResult> EnsureMetadataAsync(CriteriaSeedHeader header,
        Uuid bindingEventId, Uuid operationId, CancellationToken ct);

    /// <summary>Record or verify one independently owned support disclosure.</summary>
    ValueTask<CriteriaSeedWriteResult> EnsureDisclosureAsync(CriteriaSeedHeader header,
        Uuid bindingEventId, Uuid operationId, CriteriaSupportGap gap, CancellationToken ct);

    /// <summary>
    /// Record or verify one entry against its exact original source fact. For a
    /// point of focus, prove its exact accepted parent incarnation separately.
    /// A later authorized summary correction is not an initial-seed conflict.
    /// </summary>
    ValueTask<CriteriaSeedWriteResult> EnsureEntryAsync(CriteriaSeedHeader header,
        Uuid bindingEventId, Uuid operationId, Criterion entry, CancellationToken ct);

    /// <summary>
    /// Use normal claim and witness authority to publish; a sent request or
    /// unknown participant outcome cannot be reported as confirmed.
    /// </summary>
    ValueTask<CriteriaSeedWriteResult> PublishAsync(CriteriaSeedHeader header,
        Uuid bindingEventId, Uuid operationId, string witnessIdentifier, CancellationToken ct);

    /// <summary>
    /// Verify each expected seed fact and outcome independently and record one
    /// scalar import completion bound to the exact published seed. This does not
    /// claim that no other independently authored entries exist. A fingerprint,
    /// count or lagging projection alone cannot establish import completion.
    /// </summary>
    ValueTask<CriteriaSeedWriteResult> ConfirmCoverageAsync(CriteriaSeedHeader header,
        Uuid bindingEventId, CancellationToken ct);
}
