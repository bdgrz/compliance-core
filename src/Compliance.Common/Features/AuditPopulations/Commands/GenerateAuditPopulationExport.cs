using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Builds a deterministic export of one frozen population version into governed evidence storage.</summary>
[Discriminator("bdgrz.audit-populations.generate-audit-population-export", 2)]
public sealed record GenerateAuditPopulationExport(Uuid TenantId, Uuid ProgramId, Uuid PopulationId,
    long Version, string Format, string ExportKey)
    : IRequest<AuditPopulationExportReceipt>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
