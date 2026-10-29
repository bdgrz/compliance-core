using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Work.Models;

public sealed record ControlOperatingPlanLineWorkState(Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, long Revision, Uuid? PendingPlanVersionId);
