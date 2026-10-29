using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Features.Tenants.Models;

/// <summary>
///     The exact recipient, purpose and scopes an invitation offers. Guest program scopes are normalized to ordinal
///     identifier order so equal scopes always freeze the same manifest.
/// </summary>
public sealed record TenantInvitationTerms(string EmailAddress, string Affiliation, bool Administrator,
    string? BuiltInRole, IReadOnlyList<Uuid>? GuestProgramIds, DateOnly? GuestExpiresOn, Uuid InvitedBy,
    string? InvitedByDisplay)
{
    /// <summary>Builds the terms of a non-guest invitation.</summary>
    public static TenantInvitationTerms ForMember(string emailAddress, string affiliation, bool administrator,
        string? builtInRole, Uuid invitedBy) =>
        new(emailAddress, affiliation, administrator, builtInRole, null, null, invitedBy, null);

    /// <summary>Builds the terms of a guest invitation with normalized, de-duplicated-or-rejected program scopes.</summary>
    public static TenantInvitationTerms ForGuest(string emailAddress, IReadOnlyList<Uuid>? programIds,
        DateOnly expiresOn, Uuid invitedBy, string? invitedByDisplay) =>
        new(emailAddress, "guest", false, null,
            programIds?.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray(), expiresOn, invitedBy,
            invitedByDisplay);
}
