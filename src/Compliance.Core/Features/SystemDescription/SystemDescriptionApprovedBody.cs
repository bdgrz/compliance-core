using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Persists an approval of one exact narrative version.</summary>
public sealed record SystemDescriptionApprovedBody(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SystemDescriptionId,
    long AggregateRevision,
    SystemDescriptionApprovalView Approval,
    DateTimeOffset OccurredAt);
