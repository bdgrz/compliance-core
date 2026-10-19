using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Administrative view of invitations; tokens and token hashes never leave the aggregate.</summary>
public sealed record TenantInvitationView(Uuid TenantId, string EmailAddress,
    string Affiliation, bool Administrator, string? BuiltInRole, string Status,
    DateTimeOffset ExpiresAt, Uuid InvitedBy, Uuid? AcceptedUserId,
    IReadOnlyList<Uuid>? GuestProgramIds = null, DateOnly? GuestExpiresOn = null)
{
    public string DeliveryStatus { get; init; } = "pending";
}
