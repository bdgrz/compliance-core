using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the AdvisorHandoffClosed domain fact for an advisor handoff.</summary>
[Discriminator("bdgrz.advisor-handoff.closed", 1)]
public sealed record AdvisorHandoffClosed(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, long Revision, string Rationale, ActorReference ClosedBy, DateTimeOffset ClosedAt) : DomainEvent;
