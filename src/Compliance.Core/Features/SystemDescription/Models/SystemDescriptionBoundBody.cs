using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.SystemDescription.Models;

/// <summary>Persists the exact approved version's examination snapshot binding.</summary>
public sealed record SystemDescriptionBoundBody(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionBindingView Binding,
    DateTimeOffset OccurredAt);
