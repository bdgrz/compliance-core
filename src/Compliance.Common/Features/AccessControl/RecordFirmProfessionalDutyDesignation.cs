using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Relays one signed active assertion. <paramref name="AssertionId" /> is the logical operation identity and must
///     equal the verified assertion, so an exact retry replays the original 202 acknowledgment.
///     <paramref name="ExpectedRevision" /> is the revision of the designation owner for the assertion's subject, duty
///     and scope.
/// </summary>
[Discriminator("bdgrz.firm.professional-duty.designate", 1)]
public sealed record RecordFirmProfessionalDutyDesignation(Uuid DesignationId, Uuid AssertionId,
    string SignedAssertion, long ExpectedRevision)
    : IRequest<OperationAcceptedResponse>, IProfessionalDutyAdministrationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => AssertionId;
}
