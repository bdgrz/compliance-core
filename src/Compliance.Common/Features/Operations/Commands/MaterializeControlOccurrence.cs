using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>
///     Materializes one selected occurrence of the <c>ControlOccurrenceFanOut</c>. <paramref name="Source" /> is the
///     <see cref="DueControlOccurrencesGenerated" /> fact that selected it, <paramref name="Intent" /> the frozen
///     creation intent and <paramref name="OccurrenceId" /> the canonical identity derived from the tenant, program,
///     control and <paramref name="BusinessWindow" /> only. Two generators that select one window therefore append one
///     occurrence.
/// </summary>
[Discriminator("bdgrz.control-occurrence.materialize", 1)]
public sealed record MaterializeControlOccurrence(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid ProgramId, Uuid ControlId, ControlBusinessWindow BusinessWindow, Uuid OccurrenceId)
    : IRequest<ReceiverOutcome>, IControlOccurrenceReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
