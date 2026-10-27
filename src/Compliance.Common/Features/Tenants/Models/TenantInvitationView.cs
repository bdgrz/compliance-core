using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Models;

/// <summary>Administrative view of invitations; tokens and token hashes never leave the aggregate.</summary>
public sealed record TenantInvitationView(Uuid TenantId, string EmailAddress,
    string Affiliation, bool Administrator, string? BuiltInRole, string Status,
    DateTimeOffset ExpiresAt, Uuid InvitedBy, Uuid? AcceptedUserId,
    IReadOnlyList<Uuid>? GuestProgramIds = null, DateOnly? GuestExpiresOn = null)
{
    /// <summary>The client-chosen identity of the invitation.</summary>
    public Uuid InvitationId { get; init; }

    /// <summary>The delivery outcome, read from the delivery process: pending, delivered or failed.</summary>
    public string DeliveryStatus { get; init; } = "pending";
}
