using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;

/// <summary>Opens the exact selected versions of a draft handoff to the assigned advisor at the expected handoff revision.</summary>
[Discriminator("bdgrz.advisor-collaboration.mark-advisor-handoff-ready-for-review", 2)]
public sealed record MarkAdvisorHandoffReadyForReview(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    long ExpectedRevision) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest,
    IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
