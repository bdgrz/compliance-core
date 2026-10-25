using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>An exact admitted review that cannot decide, with its authoritative reason and affected frozen evidence.</summary>
public sealed record RiskActionReviewFailureProof(Uuid ProgramId, Uuid RiskId, Uuid ActionId, string Reason,
    Uuid? EvidenceRequestId, NativeFactReference Admission, Uuid OperationId, long Generation, ImmutableContentRef Intent);
