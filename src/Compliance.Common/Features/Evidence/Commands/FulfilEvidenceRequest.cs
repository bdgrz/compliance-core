using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Evidence.Commands;

/// <summary>
///     Answers an open request with a captured evidence artifact; the owner or a program manager may fulfil it.
///     <paramref name="ExpectedRevision" /> is the revision of the evidence request, and HTTP is the only entry point.
/// </summary>
[Discriminator("bdgrz.evidence.request.fulfil", 2)]
public sealed record FulfilEvidenceRequest(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid EvidenceRequestId,
    long ExpectedRevision, Uuid ArtifactId)
    : IRequest<EvidenceRequestView>, IControlOperationRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
