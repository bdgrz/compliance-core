using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>The authoritative permanent proof mismatch that ended a pending resolution, and the response it concerns.</summary>
public sealed record AuditorRequestFailureProof(Uuid ProgramId, Uuid RequestId, string Reason, Uuid? ResponseId,
    Uuid RootOperationId, long Generation, NativeFactReference Admission, ImmutableContentRef Intent);
