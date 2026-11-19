using System.Text.Json.Serialization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
/// Bounded evidence of one trusted source-authoritative admission. This record is
/// not itself an authorization decision and must never be accepted from a client.
/// </summary>
/// <param name="UserId">The authenticated platform user.</param>
/// <param name="ActorSubject">The authenticated actor subject.</param>
/// <param name="ActorIssuer">The authenticated actor issuer.</param>
/// <param name="OperationId">The client operation identity in its target scope.</param>
/// <param name="Target">The exact canonical target stream.</param>
/// <param name="IntentSha256">The canonical normalized authored-intent digest.</param>
/// <param name="AdmittedAt">The trusted UTC instant certified by the authority.</param>
/// <param name="PolicyGeneration">The authoritative policy generation evaluated.</param>
/// <param name="Identity">The exact identity owner source version.</param>
/// <param name="Policy">The exact policy owner source version for format 1; null for code-defined policy format 2.</param>
public sealed record AccessAdmissionEvidence(
    Uuid UserId,
    string ActorSubject,
    string ActorIssuer,
    Uuid OperationId,
    string Target,
    string IntentSha256,
    DateTimeOffset AdmittedAt,
    string PolicyGeneration,
    AccessAdmissionSourceVersion Identity,
    AccessAdmissionSourceVersion? Policy)
{
    /// <summary>Evidence format; zero is legacy, one is source policy and two is code-defined policy.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FormatVersion { get; init; }

    /// <summary>SHA-256 identity of the exact immutable policy content evaluated for format 1.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyContentSha256 { get; init; }

    /// <summary>The exact session owner when the authentication scheme has one.</summary>
    public AccessAdmissionSourceVersion? Session { get; init; }

    /// <summary>The exact active platform-operator owner when required.</summary>
    public AccessAdmissionSourceVersion? Operator { get; init; }

    /// <summary>The exact active tenant owner when required.</summary>
    public AccessAdmissionSourceVersion? Tenant { get; init; }

    /// <summary>The exact tenant member and episode owner when required.</summary>
    public AccessAdmissionSourceVersion? Member { get; init; }

    /// <summary>The exact primary-administrator owner when that path is selected.</summary>
    public AccessAdmissionSourceVersion? Primary { get; init; }

    /// <summary>The exact team-member owner when a team path is selected.</summary>
    public AccessAdmissionSourceVersion? TeamMember { get; init; }

    /// <summary>The exact team owner when a team path is selected.</summary>
    public AccessAdmissionSourceVersion? Team { get; init; }

    /// <summary>The exact team-role owner when a team path is selected.</summary>
    public AccessAdmissionSourceVersion? TeamRole { get; init; }

    /// <summary>The exact role owner when a team path is selected.</summary>
    public AccessAdmissionSourceVersion? Role { get; init; }

    /// <summary>The exact role-permission owner when a team path is selected.</summary>
    public AccessAdmissionSourceVersion? RolePermission { get; init; }

    /// <summary>The exact account veto owner when policy requires one.</summary>
    public AccessAdmissionSourceVersion? Account { get; init; }

    /// <summary>The exact global veto owner when policy requires one.</summary>
    public AccessAdmissionSourceVersion? GlobalVeto { get; init; }
}
