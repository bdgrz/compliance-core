using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists exact source reconciliation on a new immutable draft version.</summary>
[Discriminator("bdgrz.systemdescription.system-description-sources-bound", 1)]
public sealed record SystemDescriptionSourcesBound(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionVersionView Version,
    DateTimeOffset OccurredAt) : DomainEvent;
