using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Contracts;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Commands;

/// <summary>The current assignee raises work to program managers for visibility; it does not reassign.</summary>
[Discriminator("bdgrz.work.escalate", 2)]
public sealed record EscalateWorkItem(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid WorkItemId, long ExpectedRevision,
    string Reason)
    : IRequest<WorkItemDetailView>, IControlOperationRequest, ICallable, IIdentifiedRequest, IWorkAssignmentCommand
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
