using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

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
