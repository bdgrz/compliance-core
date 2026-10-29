using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.SystemDescription.Commands;

/// <summary>Identified authored intent over its exact selected owner/version scope.</summary>
[Discriminator("bdgrz.system-description.propose-system-description-successor", 2)]
public sealed record ProposeSystemDescriptionSuccessor(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, long ExpectedRevision, string ChangeReason) : IRequest, ICallable, IIdentifiedRequest, IProgramScopedRequest, IClientManagementMutationRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
