using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Contracts;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Commands;

/// <summary>The current assignee hands work to another member the source workflow would accept.</summary>
[Discriminator("bdgrz.work.delegate", 2)]
public sealed record DelegateWorkItem(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid WorkItemId, long ExpectedRevision,
    Uuid AssigneeMemberId, string Reason)
    : IRequest<WorkItemDetailView>, IControlOperationRequest, ICallable, IIdentifiedRequest, IWorkAssignmentCommand
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
