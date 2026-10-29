using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Relays one signed revocation assertion. <paramref name="AssertionId" /> is the logical operation identity and
///     must equal the verified assertion. Revocation does not require the staff member to be active.
/// </summary>
[Discriminator("bdgrz.firm.professional-duty.revoke", 1)]
public sealed record RevokeFirmProfessionalDutyDesignation(Uuid DesignationId, Uuid AssertionId,
    string SignedAssertion, long ExpectedRevision)
    : IRequest<OperationAcceptedResponse>, IProfessionalDutyAdministrationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => AssertionId;
}
