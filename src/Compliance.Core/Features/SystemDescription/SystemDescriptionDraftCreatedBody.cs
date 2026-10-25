using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists the first complete authored version of a system description.</summary>
public sealed record SystemDescriptionDraftCreatedBody(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionVersionView Version,
    DateTimeOffset OccurredAt);
