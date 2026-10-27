using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Commands;

/// <summary>Records a workforce person manually. A person need not be a platform member.</summary>
[Discriminator("bdgrz.workforce.person.record", 2)]
public sealed record RecordPerson(Uuid PersonId, Uuid TenantId, string DisplayName, string? WorkEmail = null,
    PersonalContactDetails? PersonalContact = null)
    : IRequest<PersonRegistration>, IIdentifiedRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => PersonId;
}
