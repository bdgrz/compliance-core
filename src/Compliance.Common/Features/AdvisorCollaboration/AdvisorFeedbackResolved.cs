using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the AdvisorFeedbackResolved domain fact for an advisor handoff.</summary>
[Discriminator("bdgrz.advisor-feedback.resolved", 1)]
public sealed record AdvisorFeedbackResolved(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, Uuid FeedbackId, ActorReference ResolvedBy, DateTimeOffset ResolvedAt) : DomainEvent;
