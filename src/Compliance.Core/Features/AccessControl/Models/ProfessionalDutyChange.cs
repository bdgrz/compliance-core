using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>A verified accepted change with the designation owner it was found on, or the reason it is not verified.</summary>
sealed record ProfessionalDutyChange(FirmProfessionalDutyDesignation? Owner,
    ProfessionalDutyChangeAccepted? Accepted, ProfessionalDutyChangeIntent? Intent,
    VerifiedFirmProfessionalDutyAssertion? Evidence, RequestError? Error)
{
    public static ProfessionalDutyChange Failed(RequestError error) => new(null, null, null, null, error);
}
