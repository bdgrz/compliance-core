using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

[Discriminator("bdgrz.readiness.assessment.recorded", 1)]
public sealed record ReadinessAssessmentRecorded(Uuid TenantId, Uuid ProgramId, long Revision,
    Uuid AssessmentId, string RuleVersion, DateTimeOffset AsOf, Uuid? EditionId,
    string InputFingerprint, IReadOnlyList<ReadinessInputView> Inputs,
    IReadOnlyList<ReadinessFindingView> Findings, IReadOnlyList<ReadinessGapView> Gaps,
    Uuid RunnerMemberId, ActorReference RunBy, DateTimeOffset RunAt) : DomainEvent
{
    IReadOnlyList<ReadinessAccessReviewScopeView>? _accessReviewScopes;

    public IReadOnlyList<ReadinessAccessReviewScopeView> AccessReviewScopes
    {
        get => _accessReviewScopes ?? [];
        init => _accessReviewScopes = value;
    }
}
