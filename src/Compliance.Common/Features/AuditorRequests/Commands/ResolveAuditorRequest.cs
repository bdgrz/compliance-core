using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Accepts the resolution of a request. <paramref name="ResponseIds" /> must be nonempty, duplicate free and exactly
///     equal to every response admitted on the request; the complete selection is frozen and further response, comment
///     and sample admission is sealed. <c>AuditorRequestResolutionProcess</c> then holds every response, proves its
///     accepted external disposition and appends the resolution, so the first response is a 202.
/// </summary>
[Discriminator("bdgrz.auditor-requests.resolve-auditor-request", 2)]
public sealed record ResolveAuditorRequest(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RequestId,
    string Resolution, long ExpectedRevision, IReadOnlyList<Uuid> ResponseIds)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
