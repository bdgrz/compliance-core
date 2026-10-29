using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

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
