using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Contracts;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Commands;

/// <summary>Claims unassigned team work for the acting team member, who must be eligible under the source workflow.</summary>
[Discriminator("bdgrz.work.claim", 2)]
public sealed record ClaimWorkItem(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid WorkItemId, long ExpectedRevision)
    : IRequest<WorkItemDetailView>, IControlOperationRequest, ICallable, IIdentifiedRequest, IWorkAssignmentCommand
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
