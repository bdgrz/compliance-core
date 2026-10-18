using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Complete attributed feedback history on one exact record version.</summary>
public sealed record AdvisorFeedbackView(Uuid TenantId, Uuid ProgramId, Uuid AdvisorFeedbackId,
    Uuid HandoffId, Uuid RecordId, Uuid RecordVersionId, string Kind, string Body,
    ActorReference Author, DateTimeOffset CreatedAt,
    IReadOnlyList<AdvisorFeedbackCorrectionView> Corrections,
    IReadOnlyList<AdvisorFeedbackResponseView> Responses, DateTimeOffset? ResolvedAt,
    bool IsCurrent, AdvisorRecordVersion? SourceVersion = null);
