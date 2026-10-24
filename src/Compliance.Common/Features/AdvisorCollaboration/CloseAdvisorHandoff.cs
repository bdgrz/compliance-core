using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Closes an open handoff at the expected handoff revision; feedback admitted earlier is never discarded.</summary>
[Discriminator("bdgrz.advisor-collaboration.close-advisor-handoff", 2)]
public sealed record CloseAdvisorHandoff(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    string Rationale, long ExpectedRevision)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
