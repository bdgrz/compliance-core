using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Responsibilities.Commands;

[Discriminator("bdgrz.responsibility.revoke", 2)]
public sealed record RevokeResponsibility(Uuid CommandId, Uuid TenantId, string RecordType, Uuid RecordId,
    Uuid VersionId, long ScopeRevision, Uuid AssignmentId, string Reason)
    : IRequest, IRbacManagementRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;

    [JsonIgnore]
    public ResponsibilityScope Scope => new(RecordType, RecordId, VersionId, ScopeRevision);
}
