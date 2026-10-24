using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     The default corrective action a routed finding admits atomically with its raise: its own provenance, whose
///     logical id is the deterministic action id, and the frozen admission intent.
/// </summary>
public sealed record InitialActionAdmission(OperationProvenance Operation, ImmutableContentRef Intent);
