using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Bounded current author and identity authority; draft narrative bytes remain immutable content.</summary>
public sealed record RiskDraftAuthorityHeader(Uuid TenantId, Uuid ProgramId, Uuid RiskId, string Identifier,
    long Revision, Uuid ActorMemberId, string ActorDisplay, ActorReference Actor, DateTimeOffset ChangedAt);
