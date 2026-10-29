using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>
///     Corrects a submitted attestation by a new version with a reason; HTTP-only. <paramref name="CommandId" /> is
///     the logical operation identity.
/// </summary>
[Discriminator("bdgrz.control.occurrence.correct", 2)]
public sealed record CorrectControlAttestation(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid OccurrenceId, long ExpectedRevision, string Result, DateTimeOffset PerformedAt, DateOnly? CoveredFrom,
    DateOnly? CoveredUntil, string? Notes, string? Rationale, IReadOnlyList<EvidenceReference> Evidence,
    string CorrectionReason, Uuid? PerformedByPersonId = null)
    : IRequest<ControlOccurrenceView>, IControlOperationRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
