using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists the exact approved version's examination snapshot binding.</summary>
public sealed record SystemDescriptionBoundBody(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionBindingView Binding,
    DateTimeOffset OccurredAt);
