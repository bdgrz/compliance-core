using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Identified authored intent over its exact selected owner/version scope.</summary>
[Discriminator("bdgrz.system-description.approve-system-description", 2)]
public sealed record ApproveSystemDescription(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, long Version, string Rationale, long ExpectedRevision) : IRequest, ICallable, IIdentifiedRequest, IProgramScopedRequest, IClientManagementMutationRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
