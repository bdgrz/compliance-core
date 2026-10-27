using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Records that an event-driven or ad hoc control performance is due. The occurrence identity derives from the
///     tenant, program, control and <paramref name="BusinessWindow" /> only; <paramref name="CommandId" />
///     distinguishes authored attempts, and a trigger or date never creates a second occurrence for one window.
///     <paramref name="ExpectedRevision" /> is the occurrence's logical revision, zero when it is not materialized.
/// </summary>
[Discriminator("bdgrz.control.occurrence.open", 2)]
public sealed record OpenControlOccurrence(Uuid TenantId, Uuid ProgramId, Uuid ControlId, string Trigger,
    DateOnly OccurredOn, long ExpectedRevision, Uuid CommandId, ControlBusinessWindow BusinessWindow)
    : IRequest<ControlOccurrenceView>, IControlOperationRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
