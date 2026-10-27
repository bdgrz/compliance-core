using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

static class FirmProfessionalDutyDirectorySchema
{
    public static readonly KvDirectoryIndex<FirmProfessionalDutyDesignationView> ByOwner = new(
        "by_designation_owner", 1,
        static designation => [KeyOf(designation).ToString()]);

    /// <summary>Current view of every designation owner, keyed by the owner identity (subject, duty, scope).</summary>
    public static readonly KvDirectory<FirmProfessionalDutyDesignationView, Uuid> Designations = new(
        "firm-professional-duties-by-owner",
        ComplianceCoreJsonContext.Default.FirmProfessionalDutyDesignationView,
        static designation => KeyOf(designation),
        static owner => [owner.ToString()],
        [ByOwner]);

    static Uuid KeyOf(FirmProfessionalDutyDesignationView designation) =>
        FirmProfessionalDutyDesignation.IdentityOf(designation.UserId, designation.Duty, designation.TenantId);
}
