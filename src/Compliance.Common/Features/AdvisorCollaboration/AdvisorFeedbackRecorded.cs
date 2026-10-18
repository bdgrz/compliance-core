using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the AdvisorFeedbackRecorded domain fact for an advisor handoff.</summary>
[Discriminator("bdgrz.advisor-feedback.recorded", 1)]
public sealed record AdvisorFeedbackRecorded(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, Uuid FeedbackId, Uuid RecordId, Uuid RecordVersionId, string Kind, string Body, ActorReference Author, DateTimeOffset CreatedAt) : DomainEvent;
