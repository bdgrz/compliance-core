using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists the exact approved version's examination snapshot binding.</summary>
[Discriminator("bdgrz.systemdescription.system-description-bound", 1)]
public sealed record SystemDescriptionBound(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionBindingView Binding,
    DateTimeOffset OccurredAt) : DomainEvent;
