using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records an exact frozen population version selected for one auditor request.</summary>
[Discriminator("bdgrz.audit-populations.record-sample-selection", 1)]
public sealed record RecordSampleSelection(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid ExaminationId,
    Uuid AuditorRequestId,
    long ExpectedRevision,
    Uuid SelectionId,
    Uuid PopulationId,
    int PopulationVersion,
    IReadOnlyList<Uuid> PopulationRowIds,
    string SelectionMethod,
    string Source,
    string ExternalReference)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
