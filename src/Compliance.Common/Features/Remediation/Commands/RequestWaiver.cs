using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Remediation.Contracts;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Commands;

[Discriminator("bdgrz.waiver.request", 1)]
public sealed record RequestWaiver(Uuid WaiverId, Uuid TenantId, string Rule,
    IReadOnlyList<WaiverScopeRecord> Scope, string Rationale, string CompensatingAction,
    DateTimeOffset ExpiresAt) : IRequest<WaiverRegistration>, IIdentifiedRequest,
    IWaiverManagementRequest, IClientManagementMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => WaiverId;
}
