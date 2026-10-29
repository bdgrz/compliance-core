using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Examinations.Models;

sealed record ReviewedSystemRevisionSourceContent(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long Revision, long? LegacyApplicationRevision, string ChangeKind,
    string Name, string Kind, string? AccessBoundaryReference, string? SourceIdentifier,
    string? Rationale, Uuid LastChangedByMemberId, string LastChangedByDisplay,
    ActorReference Actor, DateTimeOffset LastChangedAt);

sealed record ReviewedSystemRetirementSourceContent(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long Revision, DateTimeOffset EffectiveAt, string Reason,
    Uuid LastChangedByMemberId, string LastChangedByDisplay, ActorReference Actor,
    DateTimeOffset LastChangedAt);
