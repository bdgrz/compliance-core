namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Verified transient staff decisions; history never lives in the compact owner.</summary>
public sealed class FirmStaffHistorySnapshot(IReadOnlyList<FirmStaffChangeRecorded> changes)
{
    public IReadOnlyList<FirmStaffChangeRecorded> Changes { get; } = changes;

    public bool Matches(FirmStaffChangeRecorded change) => change.Metadata is not null &&
        Changes.Any(actual => actual.Metadata == change.Metadata && actual == change);
}
