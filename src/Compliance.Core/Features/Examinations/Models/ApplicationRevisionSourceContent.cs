using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Examinations.Models;

sealed record ApplicationRevisionSourceContent(Uuid TenantId, Uuid ApplicationId,
    long Revision, string Name, string Purpose, string? OwnerReference, string SourceKind,
    string SourceIdentifier, bool HasSystemInstances, IReadOnlyList<string> Unresolved,
    Uuid LastChangedByMemberId, string LastChangedByDisplay, ActorReference Actor,
    DateTimeOffset LastChangedAt, string ChangeKind, Uuid? SystemInstanceId,
    SystemInstanceView? SystemInstance, string? Classification,
    string? ApplicationKind, string? Criticality, bool IsRestricted,
    Uuid? SystemOwnerPersonId, Uuid? AccessOwnerPersonId);
