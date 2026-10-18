using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the AdvisorHandoffRevoked domain fact for an advisor handoff.</summary>
[Discriminator("bdgrz.advisor-handoff.revoked", 1)]
public sealed record AdvisorHandoffRevoked(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, long Revision, string Rationale, ActorReference RevokedBy, DateTimeOffset RevokedAt) : DomainEvent;
