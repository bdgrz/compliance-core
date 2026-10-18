using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the AdvisorFeedbackCorrected domain fact for an advisor handoff.</summary>
[Discriminator("bdgrz.advisor-feedback.corrected", 1)]
public sealed record AdvisorFeedbackCorrected(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, Uuid FeedbackId, Uuid CorrectionId, string CorrectedBody, string CorrectionReason, ActorReference CorrectedBy, DateTimeOffset CorrectedAt) : DomainEvent;
