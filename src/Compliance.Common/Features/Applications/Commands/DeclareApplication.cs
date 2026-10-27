using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Commands;

/// <summary>
///     Declares an application. <paramref name="ApplicationId" /> is the client-chosen immutable creation identity
///     and the logical operation: an exact retry returns the original registration and a changed creation conflicts.
/// </summary>
[Discriminator("bdgrz.application.declare", 1)]
public sealed record DeclareApplication(Uuid ApplicationId, Uuid TenantId, string Name, string Purpose,
    string? OwnerReference = null, string? Classification = null, Uuid? SystemOwnerPersonId = null,
    Uuid? AccessOwnerPersonId = null, bool IsRestricted = false,
    string? ApplicationKind = null, string? Criticality = null)
    : IRequest<ApplicationRegistration>, IApplicationInventoryWriteRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => ApplicationId;
}
