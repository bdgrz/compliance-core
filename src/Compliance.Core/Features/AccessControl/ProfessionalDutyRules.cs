using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The designation rules of a professional-duty change: the exact predecessor, the monotonic signed authority
///     revision, the stable signed subject, and the complete designation content each change produces. Signed validity
///     is always judged at the declared decision instant frozen in the change intent.
/// </summary>
static class ProfessionalDutyRules
{
    public const string RevokedReason = "Revoked by signed authority assertion";
    public const string RefreshedReason = "Verified authority assertion refreshed";

    public static bool IsSupportedDutyScope(string duty, Uuid? tenantId) => duty switch
    {
        FirmProfessionalDuty.EngagementPartner => tenantId is { } tenant && tenant != Uuid.Empty,
        FirmProfessionalDuty.RuleRatifier => tenantId is null,
        _ => false,
    };

    public static bool ValidAssertion(VerifiedFirmProfessionalDutyAssertion? evidence, string signedAssertion) =>
        evidence is not null && evidence.AssertionId != Uuid.Empty &&
        evidence.DesignationId != Uuid.Empty && evidence.UserId != Uuid.Empty &&
        evidence.AuthorityRevision > 0 && evidence.IssuedAt != default && evidence.NotBefore != default &&
        evidence.ExpiresAt > evidence.NotBefore && !string.IsNullOrWhiteSpace(evidence.Issuer) &&
        !string.IsNullOrWhiteSpace(evidence.Audience) && Bounded(signedAssertion, 16_384) &&
        ValidDigest(evidence.EvidenceDigest) && EvidenceDigest(signedAssertion) == evidence.EvidenceDigest &&
        IsSupportedDutyScope(evidence.Duty, evidence.TenantId);

    /// <summary>True when the signed validity interval contains the declared decision instant.</summary>
    public static bool ValidAt(VerifiedFirmProfessionalDutyAssertion evidence, DateTimeOffset decidedAt) =>
        evidence.IssuedAt <= decidedAt && evidence.NotBefore <= decidedAt && evidence.ExpiresAt > decidedAt;

    /// <summary>Checks that the assertion supersedes the exact current designation, or starts a new one.</summary>
    public static Result CheckChain(ProfessionalDutyChangeKind kind, VerifiedFirmProfessionalDutyAssertion evidence,
        FirmProfessionalDutyDesignationView? current)
    {
        if (kind == ProfessionalDutyChangeKind.Revoke)
            return current is { IsActive: true } active && active.DesignationId == evidence.DesignationId &&
                   active.AuthorityIssuer == evidence.Issuer && active.UserId == evidence.UserId &&
                   active.Duty == evidence.Duty && active.TenantId == evidence.TenantId &&
                   evidence.SupersedesAssertionId == active.AssertionId &&
                   evidence.AuthorityRevision > active.AuthorityRevision
                ? Result.Success
                : Conflict("Reload the professional-duty designation; the signed revocation must supersede the exact active authority revision.");

        if (current is null || current.DesignationId != evidence.DesignationId)
            return evidence.SupersedesAssertionId is not null || current is { IsActive: true }
                ? Conflict("The signed assertion conflicts with another active designation or names an unknown predecessor.")
                : Result.Success;
        return current.AuthorityIssuer == evidence.Issuer && evidence.SupersedesAssertionId == current.AssertionId &&
               evidence.AuthorityRevision > current.AuthorityRevision
            ? Result.Success
            : Conflict("The signed assertion does not supersede the current authority record for this designation.");
    }

    /// <summary>The designation a recording produces at <paramref name="revision" />.</summary>
    public static FirmProfessionalDutyDesignationView Recorded(FirmProfessionalDutyDesignationView? current,
        VerifiedFirmProfessionalDutyAssertion evidence, Uuid staffMemberId, long staffRevision, long revision,
        ActorReference actor, DateTimeOffset decidedAt)
    {
        var refresh = current is not null && current.DesignationId == evidence.DesignationId;
        var view = new FirmProfessionalDutyDesignationView(evidence.DesignationId, staffMemberId, evidence.UserId,
            evidence.Duty, evidence.TenantId, SourceReference(evidence.Issuer, evidence.DesignationId), staffRevision,
            true, revision, refresh ? current!.DesignatedBy : actor, refresh ? current!.DesignatedAt : decidedAt,
            actor, decidedAt, refresh ? RefreshedReason : null, evidence.Issuer, evidence.AssertionId,
            evidence.AuthorityRevision, evidence.IssuedAt, evidence.NotBefore, evidence.ExpiresAt,
            evidence.EvidenceDigest, "active", evidence.SupersedesAssertionId);
        return view with
        {
            ContinuityStartRevision = refresh && ContinuesActiveAuthority(current!, view) &&
                                      current!.ContinuityStartRevision > 0
                ? current.ContinuityStartRevision
                : revision,
        };
    }

    /// <summary>The designation a revocation produces at <paramref name="revision" />.</summary>
    public static FirmProfessionalDutyDesignationView Revoked(FirmProfessionalDutyDesignationView current,
        VerifiedFirmProfessionalDutyAssertion evidence, long revision, ActorReference actor, DateTimeOffset decidedAt) =>
        current with
        {
            IsActive = false,
            Revision = revision,
            AuthorityRevision = evidence.AuthorityRevision,
            AssertionId = evidence.AssertionId,
            AssertionIssuedAt = evidence.IssuedAt,
            NotBefore = evidence.NotBefore,
            ExpiresAt = evidence.ExpiresAt,
            EvidenceDigest = evidence.EvidenceDigest,
            AuthorityStatus = evidence.Status,
            SupersedesAssertionId = evidence.SupersedesAssertionId,
            LastChangedBy = actor,
            LastChangedAt = decidedAt,
            ChangeReason = RevokedReason,
            ContinuityStartRevision = 0,
        };

    public static string EvidenceDigest(string signedAssertion) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(signedAssertion)));

    /// <summary>The reservation key of the staff use a recording holds on the staff record.</summary>
    public static ReservationKey StaffUseKey(Uuid assertionId, EventStreamAddress designation,
        SourceUseRequirement requirement) => new(assertionId, designation, (ulong)ProfessionalDutyEffects.Generation,
        requirement.SourceOwner, SourceUseReservations.PurposeName(requirement.Purpose));

    static bool ContinuesActiveAuthority(FirmProfessionalDutyDesignationView previous,
        FirmProfessionalDutyDesignationView current) => previous is { IsActive: true, AuthorityStatus: "active" } &&
        previous.DesignationId == current.DesignationId && previous.UserId == current.UserId &&
        previous.StaffMemberId == current.StaffMemberId &&
        previous.DirectoryStaffRevision == current.DirectoryStaffRevision &&
        previous.Duty == current.Duty && previous.TenantId == current.TenantId &&
        previous.AuthorityIssuer == current.AuthorityIssuer &&
        previous.AssertionIssuedAt <= previous.LastChangedAt && previous.NotBefore <= previous.LastChangedAt &&
        previous.ExpiresAt > current.LastChangedAt && current.AssertionIssuedAt <= current.LastChangedAt &&
        current.NotBefore <= current.LastChangedAt;

    static string SourceReference(string issuer, Uuid designationId) =>
        $"{issuer.TrimEnd('/')}/professional-duties/{designationId}";

    static bool ValidDigest(string value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static bool Bounded(string value, int maximum) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximum && !value.Any(char.IsControl) && value == value.Trim();

    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
