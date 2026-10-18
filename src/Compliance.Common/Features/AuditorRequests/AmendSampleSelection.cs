using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Appends a linked sample selection for a later frozen population version.</summary>
[Discriminator("bdgrz.audit-populations.amend-sample-selection", 1)]
public sealed record AmendSampleSelection(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid ExaminationId,
    Uuid AuditorRequestId,
    long ExpectedRevision,
    Uuid SelectionId,
    Uuid SupersedesSelectionId,
    Uuid PopulationId,
    int PopulationVersion,
    IReadOnlyList<Uuid> PopulationRowIds,
    string SelectionMethod,
    string Source,
    string ExternalReference,
    string AmendmentReason)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
