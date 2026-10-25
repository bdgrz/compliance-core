using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists a new immutable successor draft version.</summary>
public sealed record SystemDescriptionDraftRevisedBody(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionVersionView Version,
    DateTimeOffset OccurredAt);
