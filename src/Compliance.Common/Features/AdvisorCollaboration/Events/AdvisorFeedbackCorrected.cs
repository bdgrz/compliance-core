using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Events;

/// <summary>A linked correction was appended to a feedback thread; the original contribution is preserved.</summary>
[Discriminator("bdgrz.advisor-feedback.corrected", 2)]
public sealed record AdvisorFeedbackCorrected(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, Uuid FeedbackId,
    Uuid CorrectionId, ImmutableContentRef Authored, ActorReference CorrectedBy,
    DateTimeOffset CorrectedAt, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
