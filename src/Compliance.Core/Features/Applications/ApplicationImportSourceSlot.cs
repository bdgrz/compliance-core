using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     The slot of one batch on its source: the admission fact and the settlement fact once it exists. A settlement
///     with no admission is a fence.
/// </summary>
public sealed record ApplicationImportSourceSlot(Uuid BatchId, ApplicationImportSourceAdmissionRecorded? Admitted,
    ApplicationImportSourceSettled? Settled)
{
    /// <summary>True while the batch holds the source.</summary>
    public bool IsActive => Admitted is not null && Settled is null;

    /// <summary>True when the batch was settled before it was ever admitted.</summary>
    public bool IsFence => Admitted is null && Settled is not null;
}
