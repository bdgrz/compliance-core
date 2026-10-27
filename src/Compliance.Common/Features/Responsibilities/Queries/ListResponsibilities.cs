using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Responsibilities.Queries;

[Discriminator("bdgrz.responsibilities.list", 1)]
public sealed record ListResponsibilities(Uuid TenantId, string RecordType, Uuid RecordId,
    Uuid VersionId, long ScopeRevision, long? MinimumRevision = null)
    : IRequest<ResponsibilitySetView>, ITenantAccessRequest, ICallable
{
    [JsonIgnore]
    public ResponsibilityScope Scope => new(RecordType, RecordId, VersionId, ScopeRevision);
}
