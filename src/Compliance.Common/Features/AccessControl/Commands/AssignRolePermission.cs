using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "AssignRolePermission uses the canonical RBAC relationship term.")]
[Discriminator("bdgrz.rbac.role-permission.assign", 2)]
public sealed record AssignRolePermission(Uuid TenantId, Uuid RoleId, string Permission,
    ulong ExpectedRevision)
    : IRequest, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    [JsonIgnore]
    public Uuid OperationId => Uuid.CreateVersion5(RoleId, string.Create(CultureInfo.InvariantCulture,
        $"role-permission:assign\n{TenantId}\n{Permission?.Trim().ToLowerInvariant()}\n{ExpectedRevision}"));

    Uuid IIdentifiedRequest.RequestId => OperationId;
}
