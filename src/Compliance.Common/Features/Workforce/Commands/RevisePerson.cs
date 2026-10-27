using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

[Discriminator("bdgrz.workforce.person.revise", 2)]
public sealed record RevisePerson(Uuid CommandId, Uuid TenantId, Uuid PersonId, long ExpectedRevision,
    string DisplayName, string? WorkEmail = null, PersonalContactDetails? PersonalContact = null)
    : IRequest<UnitResult>, IIdentifiedRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
