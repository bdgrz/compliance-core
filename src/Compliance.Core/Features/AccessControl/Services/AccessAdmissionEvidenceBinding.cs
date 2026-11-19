using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>
/// Checks the shape and exact intent binding of a trusted admission receipt.
/// This is not a source-authority verifier and must not be used to accept a
/// receipt supplied by a caller or an unqualified source.
/// </summary>
static class AccessAdmissionEvidenceBinding
{
    /// <summary>Checks the policy-content evidence shape stored in historical facts.</summary>
    internal static bool IsHistoricalFormat(AccessAdmissionEvidence? evidence) => evidence is not null &&
        (evidence.FormatVersion == 0 && evidence.PolicyContentSha256 is null ||
         evidence.FormatVersion == 1 && IsDigest(evidence.PolicyContentSha256) && Present(evidence.Policy) ||
         evidence.FormatVersion == 2 && CodePolicy(evidence) && evidence.Policy is null);

    /// <summary>New admissions and new target facts require content-bound version 1.</summary>
    internal static bool IsCurrentFormat(AccessAdmissionEvidence? evidence) =>
        evidence is not null && (evidence.FormatVersion == 1 && Present(evidence.Policy) ||
            evidence.FormatVersion == 2 && evidence.Policy is null && CodePolicy(evidence)) && IsDigest(evidence.PolicyContentSha256);

    internal static bool BindsCriteriaOperator(AccessAdmissionEvidence? evidence,
        Uuid userId, ActorAttribution actor, Uuid operationId, string target, string intentSha256) =>
        Matches(evidence, userId, actor, operationId, target, intentSha256) &&
        (Present(evidence!.Operator) || evidence.FormatVersion == 2 &&
            evidence.PolicyGeneration == CodePolicyAdmissionAuthority.DeveloperPolicyGeneration &&
            evidence.PolicyContentSha256 == CodePolicyAdmissionAuthority.DeveloperPolicyDigest);

    internal static bool BindsProgramCreation(AccessAdmissionEvidence? evidence,
        Uuid userId, ActorAttribution actor, Uuid programId, string target, string intentSha256)
    {
        if (!Matches(evidence, userId, actor, programId, target, intentSha256) ||
            !Present(evidence!.Tenant) || !Present(evidence.Member))
            return false;

        var primaryPath = Present(evidence.Primary);
        var teamPath = Present(evidence.TeamMember) && Present(evidence.Team) &&
                       Present(evidence.TeamRole) && Present(evidence.Role) &&
                       Present(evidence.RolePermission);
        return primaryPath || teamPath;
    }

    internal static bool BindsProgramSelection(AccessAdmissionEvidence? evidence,
        Uuid userId, ActorAttribution actor, Uuid operationId, string target, string intentSha256) =>
        BindsProgramCreation(evidence, userId, actor, operationId, target, intentSha256);

    static bool Matches(AccessAdmissionEvidence? evidence, Uuid userId, ActorAttribution? actor,
        Uuid operationId, string? target, string? intentSha256) =>
        IsHistoricalFormat(evidence) && evidence is not null && userId != Uuid.Empty && evidence.UserId == userId &&
        actor is not null && !string.IsNullOrWhiteSpace(actor.Subject) &&
        !string.IsNullOrWhiteSpace(actor.Issuer) &&
        string.Equals(evidence.ActorSubject, actor.Subject, StringComparison.Ordinal) &&
        string.Equals(evidence.ActorIssuer, actor.Issuer, StringComparison.Ordinal) &&
        operationId != Uuid.Empty && evidence.OperationId == operationId &&
        CanonicalStream(target) && string.Equals(evidence.Target, target, StringComparison.Ordinal) &&
        IsDigest(intentSha256) && string.Equals(evidence.IntentSha256, intentSha256, StringComparison.Ordinal) &&
        evidence.AdmittedAt != default && evidence.AdmittedAt.Offset == TimeSpan.Zero &&
        !string.IsNullOrWhiteSpace(evidence.PolicyGeneration) &&
        Present(evidence.Identity) && (evidence.FormatVersion == 2 ? evidence.Policy is null : Present(evidence.Policy)) &&
        ValidOptional(evidence.Session) && ValidOptional(evidence.Operator) &&
        ValidOptional(evidence.Tenant) && ValidOptional(evidence.Member) &&
        ValidOptional(evidence.Primary) && ValidOptional(evidence.TeamMember) &&
        ValidOptional(evidence.Team) && ValidOptional(evidence.TeamRole) &&
        ValidOptional(evidence.Role) && ValidOptional(evidence.RolePermission) &&
        ValidOptional(evidence.Account) && ValidOptional(evidence.GlobalVeto);

    static bool Present(AccessAdmissionSourceVersion? source) => source is not null &&
        source.Position > 0 && !string.IsNullOrWhiteSpace(source.Generation) && CanonicalStream(source.Stream);

    static bool ValidOptional(AccessAdmissionSourceVersion? source) => source is null ||
        !string.IsNullOrWhiteSpace(source.Generation) && CanonicalStream(source.Stream);

    static bool CodePolicy(AccessAdmissionEvidence evidence) =>
        evidence.PolicyGeneration == CodePolicyAdmissionAuthority.PolicyGeneration &&
            evidence.PolicyContentSha256 == CodePolicyAdmissionAuthority.PolicyDigest ||
        evidence.PolicyGeneration == CodePolicyAdmissionAuthority.DeveloperPolicyGeneration &&
            evidence.PolicyContentSha256 == CodePolicyAdmissionAuthority.DeveloperPolicyDigest;

    static bool IsDigest(string? value) => value is { Length: 64 } &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static bool CanonicalStream(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        try
        {
            return string.Equals(EventStreamAddress.Parse(value).ToString(), value, StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
