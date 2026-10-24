using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>The authoritative permanent proof mismatch that ended a pending closure, and the action it concerns.</summary>
public sealed record FindingClosureFailureProof(Uuid ProgramId, Uuid FindingId, string Reason, Uuid? ActionId);
