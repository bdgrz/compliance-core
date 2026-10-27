using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Contracts;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Commands;

/// <summary>A program manager assigns or reassigns work to a member the source workflow would accept.</summary>
[Discriminator("bdgrz.work.assign", 2)]
public sealed record AssignWorkItem(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid WorkItemId, long ExpectedRevision,
    Uuid AssigneeMemberId, string? Reason = null)
    : IRequest<WorkItemDetailView>, IControlOperationRequest, ICallable, IIdentifiedRequest, IWorkAssignmentCommand
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
