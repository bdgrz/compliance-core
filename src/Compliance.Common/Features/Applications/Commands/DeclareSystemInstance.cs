using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
namespace Bdgrz.Compliance.Features.Applications.Commands;

/// <summary>
///     Declares a system instance. <paramref name="SystemInstanceId" /> is the immutable creation identity and the
///     logical operation: an exact retry returns the original registration.
/// </summary>
[Discriminator("bdgrz.system_instance.declare", 1)]
public sealed record DeclareSystemInstance(Uuid SystemInstanceId, Uuid TenantId, Uuid ApplicationId,
    long ExpectedApplicationRevision, string Name, string Kind,
    string? AccessBoundaryReference = null, string? SourceIdentifier = null)
    : IRequest<SystemInstanceRegistration>, IApplicationInventoryWriteRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => SystemInstanceId;
}
