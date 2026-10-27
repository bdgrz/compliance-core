using Bdgrz.Compliance.Features.Applications.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     What one declared application recorded for one import batch: the exclusive hold, the fence that blocks a
///     delayed operation, the provisional effects of the batch's frozen group and the settlement. A fence may exist
///     without a hold. The record is rebuilt from the application's ordered stream alone.
/// </summary>
public sealed record ApplicationImportHold(Uuid BatchId, ApplicationImportTargetAuthorized? Reserved,
    ApplicationImportEffectCanceled? Fence, IReadOnlyList<ApplicationImportEffectPending> Effects,
    ApplicationImportTargetSettled? Settled)
{
    /// <summary>True while the batch holds the application and has not settled.</summary>
    public bool IsActive => Reserved is not null && Settled is null;

    /// <summary>True when the batch's provisional effects were appended.</summary>
    public bool IsApplied => Effects.Count > 0;
}
