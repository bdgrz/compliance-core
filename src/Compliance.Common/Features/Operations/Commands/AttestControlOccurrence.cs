using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>
///     The performer's personal attestation of one occurrence; HTTP-only. A member may record a
///     workforce person's off-product performance; both are attributed. <paramref name="CommandId" /> is the
///     logical operation identity.
/// </summary>
[Discriminator("bdgrz.control.occurrence.attest", 2)]
public sealed record AttestControlOccurrence(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid OccurrenceId, long ExpectedRevision, string Result, DateTimeOffset PerformedAt, DateOnly? CoveredFrom,
    DateOnly? CoveredUntil, string? Notes, string? Rationale, IReadOnlyList<EvidenceReference> Evidence,
    Uuid? PerformedByPersonId = null)
    : IRequest<ControlOccurrenceView>, IControlOperationRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
