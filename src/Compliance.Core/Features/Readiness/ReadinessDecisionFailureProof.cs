using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>The authoritative permanent proof mismatch that canceled a pending decision, with the gap that proved it when one did.</summary>
public sealed record ReadinessDecisionFailureProof(Uuid ProgramId, Uuid AssessmentId, string Reason, Uuid? GapId);
