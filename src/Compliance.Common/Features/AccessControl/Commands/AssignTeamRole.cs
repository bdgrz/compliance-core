using System.Globalization;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

[Discriminator("bdgrz.rbac.team-role.assign", 2)]
public sealed record AssignTeamRole(Uuid TenantId, Uuid TeamId, Uuid RoleId,
    ulong ExpectedRevision, AccessGrantScope? Scope = null)
    : IRequest, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    [JsonIgnore]
    public Uuid OperationId
    {
        get
        {
            var scope = Scope ?? new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId);
            var identity = string.Create(CultureInfo.InvariantCulture,
                $"team-role:assign\n{TenantId}\n{TeamId}\n{RoleId}\n{(int)scope.Kind}\n{scope.Id}\n{scope.ResourceType}\n{ExpectedRevision}");
            return Uuid.CreateVersion5(TeamId, identity);
        }
    }

    Uuid IIdentifiedRequest.RequestId => OperationId;
}
