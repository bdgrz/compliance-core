using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Applications.Commands;

/// <summary>Internal command: authorizes execution of a sealed, source-claimed batch. Its deterministic id is a source-event effect.</summary>
[Discriminator("bdgrz.application_import.execute", 2)]
public sealed record ExecuteApplicationImport(Uuid CommandId, Uuid TenantId, Uuid BatchId)
    : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
