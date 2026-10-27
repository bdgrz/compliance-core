using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>The exact requested frozen close and its retained immutable lineage on the owning period.</summary>
public sealed record ObservationPeriodCloseView(Uuid TenantId, Uuid ProgramId, Uuid ObservationPeriodCloseId,
    Uuid PeriodId, Uuid ExaminationId, TypeIIPeriodCloseSnapshotView Snapshot,
    IReadOnlyList<TypeIIPeriodCloseSnapshotView> CloseSnapshots);
