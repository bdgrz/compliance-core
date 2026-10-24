using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the intentional delivery of the exact submitted (and, when required, approved) response version.</summary>
[Discriminator("bdgrz.auditor-requests.deliver-audit-response", 2)]
public sealed record DeliverAuditResponse(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RequestId,
    Uuid ResponseId, long Version, string Recipient, string DeliveryReference, long ExpectedRevision)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
