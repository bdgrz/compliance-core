using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Tests;

/// <summary>Builds well-formed native invitation facts and terms for tests that exercise the invitation owner directly.</summary>
static class TenantInvitationFacts
{
    public static readonly DateTimeOffset At = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    static readonly ActorReference Actor = ActorReference.ForSystemProcess("system", "system");

    public static string HashOf(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>A frozen response reference of the right schema; nothing reads it back.</summary>
    public static ImmutableContentRef Response() => new(Uuid.CreateVersion4(), new string('b', 64), 2,
        "application/json", "first-response-descriptor.v1");

    /// <summary>A guest scope manifest reference whose digest stands for the scope content.</summary>
    public static ImmutableContentRef Scope(string schema, string digest = "") => new(Uuid.CreateVersion4(),
        digest.Length == 0 ? new string('5', 64) : digest, 40, "application/json", schema);

    public static OperationProvenance Provenance(TenantInvitation invitation, Uuid logicalId, string kind = "InviteOrganizationMember",
        ActorReference? actor = null) =>
        new(logicalId, logicalId, null, kind, actor ?? Actor, invitation.Stream, "initial", null, new string('a', 64), At,
            invitation.Stream, invitation.Stream);

    public static TenantInvitationTerms Member(string email = "member@example.com", string affiliation = "client_personnel",
        bool administrator = false, string? role = null, Uuid invitedBy = default) =>
        TenantInvitationTerms.ForMember(email, affiliation, administrator, role,
            invitedBy == Uuid.Empty ? Uuid.CreateVersion4() : invitedBy);

    public static TenantInvitationTerms Guest(IReadOnlyList<Uuid> programs, DateOnly expiresOn,
        string email = "guest@example.com", string? display = "Org Admin") =>
        TenantInvitationTerms.ForGuest(email, programs, expiresOn, Uuid.CreateVersion4(), display);

    /// <summary>Accepts the creation of the invitation under its own id; the token is hashed from <paramref name="token" />.</summary>
    public static Result Invite(this TenantInvitation invitation, Uuid invitationId, TenantInvitationTerms terms, string token,
        DateTimeOffset? now = null, DateTimeOffset? expiresAt = null, long generation = 1,
        ImmutableContentRef? scope = null, string? keyId = "current")
    {
        var at = now ?? At;
        return invitation.Invite(Provenance(invitation, invitationId), Response(), terms, HashOf(token), keyId,
            expiresAt ?? at.AddDays(7), at, generation,
            scope ?? (terms.Affiliation == "guest" ? Scope(TenantInvitationAdmissionSchema) : null));
    }

    /// <summary>Accepts the acceptance of the invitation under its derived acceptance id.</summary>
    public static Result Accept(this TenantInvitation invitation, Uuid invitationId, Uuid userId, string email, string token,
        DateTimeOffset? now = null, ImmutableContentRef? scope = null, ImmutableContentRef? offered = null) =>
        invitation.Accept(Provenance(invitation, TenantInvitationIds.AcceptanceOf(invitationId), "AcceptTenantInvitation"),
            Response(), userId, email, token, now ?? At, scope ?? offered ?? invitation.GuestProgramIdsManifest);

    const string TenantInvitationAdmissionSchema = TenantInvitationAdmission.GuestScopeSchema;
}
