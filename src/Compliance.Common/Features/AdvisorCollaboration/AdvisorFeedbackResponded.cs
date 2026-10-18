using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the AdvisorFeedbackResponded domain fact for an advisor handoff.</summary>
[Discriminator("bdgrz.advisor-feedback.responded", 1)]
public sealed record AdvisorFeedbackResponded(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, Uuid FeedbackId, string Response, ActorReference RespondedBy, DateTimeOffset RespondedAt) : DomainEvent;
