using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;

/// <summary>Closes an open handoff at the expected handoff revision; feedback admitted earlier is never discarded.</summary>
[Discriminator("bdgrz.advisor-collaboration.close-advisor-handoff", 2)]
public sealed record CloseAdvisorHandoff(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    string Rationale, long ExpectedRevision)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
