using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the AdvisorHandoffReadiedForReview domain fact for an advisor handoff.</summary>
[Discriminator("bdgrz.advisor-handoff.readied-for-review", 1)]
public sealed record AdvisorHandoffReadiedForReview(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, long Revision, ActorReference ReadiedBy, DateTimeOffset ReadiedAt) : DomainEvent;
