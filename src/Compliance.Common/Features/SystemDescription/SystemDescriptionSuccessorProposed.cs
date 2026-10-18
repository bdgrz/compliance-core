using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists a proposed successor to an already approved narrative version.</summary>
[Discriminator("bdgrz.systemdescription.system-description-successor-proposed", 1)]
public sealed record SystemDescriptionSuccessorProposed(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionVersionView Version,
    DateTimeOffset OccurredAt) : DomainEvent;
