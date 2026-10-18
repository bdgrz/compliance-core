using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists a reasoned disposition for one non-material period change.</summary>
[Discriminator("bdgrz.systemdescription.system-description-no-change-recorded", 1)]
public sealed record SystemDescriptionNoChangeRecorded(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionNoChangeDecisionView Decision,
    DateTimeOffset OccurredAt) : DomainEvent;
