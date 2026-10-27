using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>The exact admitted decision and frozen gap that authoritatively failed; incompatible unbound v1 is refused.</summary>
public sealed record ReadinessDecisionFailureProof(Uuid ProgramId, Uuid AssessmentId, string Reason, Uuid? GapId,
    NativeFactReference Admission, Uuid OperationId, long Generation, ImmutableContentRef Intent);
