using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed record ApplicationControlVersionReferenceState(Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, string Identifier, Uuid? CurrentVersionId, Uuid? PendingSuccessorVersionId,
    Uuid? PendingRetirementId, DateOnly? PendingRetirementEffectiveUntil, bool IsRetired,
    DateOnly? CurrentEffectiveFrom);
