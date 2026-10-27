using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>Writes the current view of one staff record into the directory read model.</summary>
public interface IFirmStaffDirectoryProjection : IProjectionStore
{
    /// <summary>Stores <paramref name="staff" /> when it is the next revision of its record.</summary>
    ValueTask ApplyAsync(FirmStaffMemberView staff, CancellationToken ct = default);
}
