using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Readiness.Models;

namespace Bdgrz.Compliance.Features.Readiness.Events;

/// <summary>A complete historical assessment and copied plans behind management's decision.</summary>
[Discriminator("bdgrz.readiness.type_i_entry.decided", 1)]
public sealed record TypeIEntryDecided(Uuid TenantId, Uuid DecisionId, Uuid ProgramId,
    Uuid AssessmentId, string RuleVersion, DateTimeOffset AsOf, string InputFingerprint,
    string Verdict, string Outcome, IReadOnlyList<ReadinessGapView> OpenGaps, string Rationale,
    Uuid? ReplacesDecisionId, Uuid DeciderMemberId, ActorReference DecidedBy,
    DateTimeOffset DecidedAt, string AuthoredIntentSha256) : DomainEvent;
