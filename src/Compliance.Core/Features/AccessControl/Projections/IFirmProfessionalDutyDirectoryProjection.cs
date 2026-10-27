using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>Writes the current designation of one owner into the catalog read model.</summary>
public interface IFirmProfessionalDutyDirectoryProjection : IProjectionStore
{
    /// <summary>Stores <paramref name="designation" /> when it is the next revision of its owner.</summary>
    ValueTask ApplyAsync(FirmProfessionalDutyDesignationView designation, CancellationToken ct = default);
}
