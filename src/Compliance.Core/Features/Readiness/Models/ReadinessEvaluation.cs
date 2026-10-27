using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>The deterministic output of <see cref="ReadinessRules" /> for one set of inputs.</summary>
public sealed record ReadinessEvaluation(IReadOnlyList<ReadinessInputView> Inputs,
    IReadOnlyList<ReadinessFindingView> Findings, IReadOnlyList<ReadinessGapView> Gaps,
    string InputFingerprint)
{
    public IReadOnlyList<ReadinessAccessReviewScopeView> AccessReviewScopes { get; init; } = [];
}
