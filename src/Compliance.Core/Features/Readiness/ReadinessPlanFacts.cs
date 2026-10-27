using Bdgrz.Compliance.Features.Readiness.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     The exact facts a decision requires of one gap plan: the plan the gap had when the decision froze it, or none for
///     an unplanned gap. A non-null <paramref name="Refusal" /> means the decision can no longer hold the plan and the
///     acquisition must fence it.
/// </summary>
public sealed record ReadinessPlanFacts(Uuid GapId, ReadinessGapPlanView? Plan, string? Refusal);
