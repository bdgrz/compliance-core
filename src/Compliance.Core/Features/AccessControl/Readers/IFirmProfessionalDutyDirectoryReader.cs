using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Readers;

/// <summary>Reads the professional-duty catalog read model; it has no size cap.</summary>
public interface IFirmProfessionalDutyDirectoryReader
{
    /// <summary>Lists every retained designation ordered by designation id.</summary>
    ValueTask<FirmProfessionalDutyCatalogView> ListAsync(CancellationToken ct = default);
}
