using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Commands;

/// <summary>Revises a system instance; <paramref name="CommandId" /> is the logical identity of the operation.</summary>
[Discriminator("bdgrz.applications.revise-system-instance", 1)]
public sealed record ReviseSystemInstance(Uuid CommandId, Uuid TenantId, Uuid ApplicationId, Uuid SystemInstanceId,
    long ExpectedRevision, string Name, string SourceIdentifier, string AccessBoundary, string Rationale)
    : IRequest, IApplicationInventoryWriteRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
