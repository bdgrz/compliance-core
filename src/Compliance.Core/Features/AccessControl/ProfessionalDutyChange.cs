using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A verified accepted change with the designation owner it was found on, or the reason it is not verified.</summary>
sealed record ProfessionalDutyChange(FirmProfessionalDutyDesignation? Owner,
    ProfessionalDutyChangeAccepted? Accepted, ProfessionalDutyChangeIntent? Intent,
    VerifiedFirmProfessionalDutyAssertion? Evidence, RequestError? Error)
{
    public static ProfessionalDutyChange Failed(RequestError error) => new(null, null, null, null, error);
}
