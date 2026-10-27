using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Readers;

/// <summary>Reads the professional-duty catalog read model; it has no size cap.</summary>
public interface IFirmProfessionalDutyDirectoryReader
{
    /// <summary>Lists every retained designation ordered by designation id.</summary>
    ValueTask<FirmProfessionalDutyCatalogView> ListAsync(CancellationToken ct = default);
}
