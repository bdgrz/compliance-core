using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

sealed record AdvisorFeedbackWorkItemProjectionState(Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    Uuid LastEventId, ulong LastAggregateVersion);
