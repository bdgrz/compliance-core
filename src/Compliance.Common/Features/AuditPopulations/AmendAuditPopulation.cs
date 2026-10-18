using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Amends a frozen population with a new source snapshot and complete changed-row content.</summary>
[Discriminator("bdgrz.audit-populations.amend-audit-population", 2)]
public sealed record AmendAuditPopulation(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid PopulationId,
    long ExpectedRevision,
    string AmendmentReason,
    AuditPopulationSourceSnapshotContent SourceSnapshot,
    IReadOnlyList<AuditPopulationRowContent> ChangedRows) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
