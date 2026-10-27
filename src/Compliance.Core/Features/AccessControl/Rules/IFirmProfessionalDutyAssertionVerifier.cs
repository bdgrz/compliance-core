using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Rules;

interface IFirmProfessionalDutyAssertionVerifier
{
    bool TryVerifyBinding(string? signedAssertion, out VerifiedFirmProfessionalDutyAssertion assertion);

    bool TryVerify(string? signedAssertion, DateTimeOffset now,
        out VerifiedFirmProfessionalDutyAssertion assertion);
}
