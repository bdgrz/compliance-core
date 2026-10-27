using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>The immutable access-review scope for one system instance at an assessment's as-of time.</summary>
public sealed record ReadinessAccessReviewScopeView(Uuid SystemInstanceId, string Status,
    bool ReviewOverdue, AccessReviewScopeDecisionView? EffectiveDecision);
