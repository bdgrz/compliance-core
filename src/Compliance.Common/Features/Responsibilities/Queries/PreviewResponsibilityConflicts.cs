using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Responsibilities.Queries;

[Discriminator("bdgrz.responsibilities.conflicts.preview", 1)]
public sealed record PreviewResponsibilityConflicts(Uuid TenantId, Uuid MemberUserId,
    string Type, string RecordType, Uuid RecordId, Uuid VersionId,
    long ScopeRevision, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil)
    : IRequest<ResponsibilityConflictPreview>, ITenantAccessRequest, ICallable
{
    [JsonIgnore]
    public ResponsibilityScope Scope => new(RecordType, RecordId, VersionId, ScopeRevision);
}
