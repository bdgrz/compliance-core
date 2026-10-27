using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>The current assignee raises work to program managers for visibility; it does not reassign.</summary>
[Discriminator("bdgrz.work.escalate", 2)]
public sealed record EscalateWorkItem(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid WorkItemId, long ExpectedRevision,
    string Reason)
    : IRequest<WorkItemDetailView>, IControlOperationRequest, ICallable, IIdentifiedRequest, IWorkAssignmentCommand
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
