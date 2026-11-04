using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Workforce.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.person.details.change", 1)]
public sealed record ChangePersonDetails(Uuid CommandId, Uuid TenantId, Uuid PersonId, long ExpectedRevision,
    string DisplayName, string? WorkEmail = null, PersonalContactDetails? PersonalContact = null)
    : IRequest<UnitResult>, IIdentifiedRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
