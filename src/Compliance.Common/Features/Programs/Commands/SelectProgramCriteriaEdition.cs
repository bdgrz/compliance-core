using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Programs.Commands;

[Discriminator("bdgrz.program.criteria.select", 1)]
public sealed record SelectProgramCriteriaEdition(Uuid CommandId, Uuid TenantId, Uuid ProgramId,
    long ExpectedRevision, Uuid EditionId) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
