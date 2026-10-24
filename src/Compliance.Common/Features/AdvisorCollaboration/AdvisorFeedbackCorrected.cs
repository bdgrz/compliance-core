using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>A linked correction was appended to a feedback thread; the original contribution is preserved.</summary>
[Discriminator("bdgrz.advisor-feedback.corrected", 1)]
public sealed record AdvisorFeedbackCorrected(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, Uuid FeedbackId,
    Uuid CorrectionId, string CorrectedBody, string CorrectionReason, ActorReference CorrectedBy,
    DateTimeOffset CorrectedAt, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
