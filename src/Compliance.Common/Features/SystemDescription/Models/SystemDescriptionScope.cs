using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription.Models;

/// <summary>Exact point-in-time context that this narrative describes.</summary>
public sealed record SystemDescriptionScope(string Stage, Uuid ExaminationId,
    Uuid? TypeIBaselineId, Uuid? TypeIIPeriodId, Uuid? CloseSnapshotId);
