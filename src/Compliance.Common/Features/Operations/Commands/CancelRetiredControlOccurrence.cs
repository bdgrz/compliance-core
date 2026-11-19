using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>Internal cancellation from an exact committed control retirement.</summary>
[Discriminator("bdgrz.control.occurrence.cancel_retired", 1)]
public sealed record CancelRetiredControlOccurrence(Uuid CommandId, Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, Uuid OccurrenceId, long ExpectedRevision, SourceFactProof<ControlRetired> Retirement)
    : IRequest, IControlOccurrenceReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
