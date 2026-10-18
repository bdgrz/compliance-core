using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists an approval of one exact narrative version.</summary>
[Discriminator("bdgrz.systemdescription.system-description-approved", 1)]
public sealed record SystemDescriptionApproved(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionApprovalView Approval,
    DateTimeOffset OccurredAt) : DomainEvent;
