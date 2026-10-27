using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>Writes the current designation of one owner into the catalog read model.</summary>
public interface IFirmProfessionalDutyDirectoryProjection : IProjectionStore
{
    /// <summary>Stores <paramref name="designation" /> when it is the next revision of its owner.</summary>
    ValueTask ApplyAsync(FirmProfessionalDutyDesignationView designation, CancellationToken ct = default);
}
