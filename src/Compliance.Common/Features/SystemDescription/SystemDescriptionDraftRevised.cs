using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists a new immutable successor draft version.</summary>
[Discriminator("bdgrz.systemdescription.system-description-draft-revised", 1)]
public sealed record SystemDescriptionDraftRevised(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionVersionView Version,
    DateTimeOffset OccurredAt) : DomainEvent;
