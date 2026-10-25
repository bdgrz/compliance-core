using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>The permanent mismatch bound to one exact closure admission, intent, root and generation.</summary>
public sealed record FindingClosureFailureProof(Uuid ProgramId, Uuid FindingId, string Reason, Uuid? ActionId,
    NativeFactReference Admission, ImmutableContentRef Intent, Uuid RootOperationId, long Generation);
