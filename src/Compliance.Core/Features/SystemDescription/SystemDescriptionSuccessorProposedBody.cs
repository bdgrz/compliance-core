using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists a proposed successor to an already approved narrative version.</summary>
public sealed record SystemDescriptionSuccessorProposedBody(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionVersionView Version,
    DateTimeOffset OccurredAt);
