namespace Bdgrz.Compliance.Features.AccessControl;

interface IFirmProfessionalDutyAssertionVerifier
{
    bool TryVerifyBinding(string? signedAssertion, out VerifiedFirmProfessionalDutyAssertion assertion);

    bool TryVerify(string? signedAssertion, DateTimeOffset now,
        out VerifiedFirmProfessionalDutyAssertion assertion);
}
