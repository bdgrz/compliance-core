using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Models;
namespace Bdgrz.Compliance.Features.Work.Models;

public sealed record ControlOccurrencePlanWorkState(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid PlanVersionId, Uuid ControlVersionId, long Revision, string Status,
    OperatingHolder Owner, OperatingHolder? BackupOwner, Uuid ReviewerMemberId,
    ControlCadence Cadence, DateOnly EffectiveFrom, DateOnly? EffectiveUntil);
