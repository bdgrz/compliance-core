using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
/// Immutable authority for one exact separation-of-duties exception. A consumer compares <see cref="ToScope"/> for
/// equality with the action it is about to take; any difference means the waiver does not authorize it.
/// </summary>
/// <param name="TenantId">The tenant that owns the waiver.</param>
/// <param name="WaiverId">The waiver aggregate identity.</param>
/// <param name="SubjectId">The member the waiver covers (its beneficiary).</param>
/// <param name="ActorId">The member who may act under the waiver.</param>
/// <param name="Action">The exact guarded action kind.</param>
/// <param name="TargetType">The guarded record type.</param>
/// <param name="TargetId">The guarded record identity.</param>
/// <param name="TargetVersionId">The guarded record version identity.</param>
/// <param name="TargetRevision">The exact logical revision of the target the waiver authorizes.</param>
/// <param name="ApprovalFact">The native waiver approval event.</param>
/// <param name="ValidFrom">Inclusive start of validity, the approval time.</param>
/// <param name="ValidUntil">Exclusive end of validity, the waiver expiry.</param>
public sealed record SeparationOfDutiesWaiverProof(
    Uuid TenantId,
    Uuid WaiverId,
    Uuid SubjectId,
    Uuid ActorId,
    string Action,
    string TargetType,
    Uuid TargetId,
    Uuid TargetVersionId,
    long TargetRevision,
    SourceFactLocator ApprovalFact,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil)
{
    /// <summary>Returns the exact comparison tuple of this proof.</summary>
    public SeparationOfDutiesWaiverProofScope ToScope() => new(TenantId, WaiverId, SubjectId, ActorId, Action,
        TargetType, TargetId, TargetVersionId, TargetRevision);
}
