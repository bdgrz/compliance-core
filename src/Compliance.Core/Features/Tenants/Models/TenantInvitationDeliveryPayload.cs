using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Features.Tenants.Models;

/// <summary>
///     The one immutable effect payload of an invitation's delivery process: the exact recipient and the digest and
///     key that identify the exact token to send. The bearer token itself is derived from the protected key when it
///     is sent and never stored.
/// </summary>
public sealed record TenantInvitationDeliveryPayload(Uuid TenantId, Uuid InvitationId, string EmailAddress,
    string TokenHash, string? TokenKeyId, DateTimeOffset ExpiresAt)
{
    /// <summary>Schema of the frozen payload content.</summary>
    public const string Schema = "tenant-invitation-delivery-payload.v1";

    /// <summary>The payload the invitation fact freezes.</summary>
    public static TenantInvitationDeliveryPayload Of(TenantMemberInvited invited) =>
        new(invited.TenantId, invited.InvitationId, invited.EmailAddress, invited.TokenHash, invited.TokenKeyId,
            invited.ExpiresAt);
}
