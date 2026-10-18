using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists the first complete authored version of a system description.</summary>
[Discriminator("bdgrz.systemdescription.system-description-draft-created", 1)]
public sealed record SystemDescriptionDraftCreated(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionVersionView Version,
    DateTimeOffset OccurredAt) : DomainEvent;
