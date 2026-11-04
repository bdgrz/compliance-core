using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;

namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>One immutable management decision; later assessments never replace it.</summary>
public sealed record TypeIEntryDecisionRecord(Uuid TenantId, Uuid ProgramId, Uuid DecisionId,
    Uuid AssessmentId, string RuleVersion, DateTimeOffset AsOf, string InputFingerprint,
    string Verdict, string Outcome, IReadOnlyList<ReadinessGapView> OpenGaps, string Rationale,
    Uuid? ReplacesDecisionId, Uuid DeciderMemberId, ActorReference DecidedBy, DateTimeOffset DecidedAt);
