using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Models;

/// <summary>Complete attributed feedback history on one exact record version.</summary>
public sealed record AdvisorFeedbackView(Uuid TenantId, Uuid ProgramId, Uuid AdvisorFeedbackId,
    Uuid HandoffId, Uuid RecordId, Uuid RecordVersionId, string Kind, string Body,
    ActorReference Author, DateTimeOffset CreatedAt,
    IReadOnlyList<AdvisorFeedbackCorrectionView> Corrections,
    IReadOnlyList<AdvisorFeedbackResponseView> Responses, DateTimeOffset? ResolvedAt,
    bool IsCurrent, AdvisorRecordVersion? SourceVersion = null,
    ActorReference? ResolvedBy = null);
