using Cntryl.Portia;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Deletes a team from a tenant. Built-in teams cannot be deleted. The team identity is also the stable
///     request identity, so retrying DELETE addresses the same deletion.
/// </summary>
[Discriminator("bdgrz.rbac.team.delete", 2)]
public sealed record DeleteTeam(Uuid TenantId, Uuid TeamId)
    : IRequest, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    [JsonIgnore]
    public Uuid OperationId => Uuid.CreateVersion5(TeamId, $"team-delete:{TenantId}");

    Uuid IIdentifiedRequest.RequestId => OperationId;
}
