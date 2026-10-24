namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reads the staff directory read model; it has no size cap.</summary>
public interface IFirmStaffDirectoryReader
{
    /// <summary>Lists registered staff ordered by staff member id, optionally only active staff.</summary>
    ValueTask<FirmStaffDirectoryView> ListAsync(bool activeOnly, CancellationToken ct = default);
}
