using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.person.revise", 2)]
public sealed record RevisePerson(Uuid CommandId, Uuid TenantId, Uuid PersonId, long ExpectedRevision,
    string DisplayName, string? WorkEmail = null, PersonalContactDetails? PersonalContact = null)
    : IRequest<UnitResult>, IIdentifiedRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
