using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities.Models;

public sealed record ResponsibilityAssignmentView(Uuid TenantId, Uuid AssignmentId,
    Uuid MemberId, ResponsibilityType Type, ResponsibilityScope Scope,
    DateTimeOffset AssignedAt, Uuid AssignedByMemberId,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil,
    DateTimeOffset? RevokedAt, Uuid RevokedByMemberId,
    IReadOnlyList<Uuid> SeparationOfDutiesWaiverIds, string AssignedByDisplay = "",
    string? RevokedByDisplay = null, string? RevocationReason = null);
