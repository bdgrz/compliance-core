using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Responsibilities.Commands;

[Discriminator("bdgrz.responsibility.assign", 2)]
public sealed record AssignResponsibility(Uuid CommandId, Uuid TenantId, Uuid MemberUserId,
    ResponsibilityType Type, string RecordType, Uuid RecordId, Uuid VersionId, long ScopeRevision,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil,
    IReadOnlyList<Uuid> SeparationOfDutiesWaiverIds) : IRequest, IRbacManagementRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;

    [JsonIgnore]
    public ResponsibilityScope Scope => new(RecordType, RecordId, VersionId, ScopeRevision);
}
