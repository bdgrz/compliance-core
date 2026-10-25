using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists a reasoned disposition for one non-material period change.</summary>
public sealed record SystemDescriptionNoChangeRecordedBody(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionNoChangeDecisionView Decision,
    DateTimeOffset OccurredAt);
