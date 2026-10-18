using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Opens one bounded advisor handoff with its exact selected record versions.</summary>
[Discriminator("bdgrz.advisor-handoff.created", 1)]
public sealed record AdvisorHandoffCreated(
    Uuid TenantId, Uuid ProgramId, Uuid HandoffId, IReadOnlyList<AdvisorRecordVersion> RecordVersions,
    string Purpose, DateOnly ExpiresOn, ActorReference CreatedBy, DateTimeOffset CreatedAt) : DomainEvent;
