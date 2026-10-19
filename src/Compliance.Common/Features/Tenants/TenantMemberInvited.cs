using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-member.invited", 1)]
public sealed record TenantMemberInvited(Uuid TenantId, string EmailAddress, string Affiliation,
    bool Administrator, string TokenHash, DateTimeOffset ExpiresAt, Uuid InvitedBy,
    string? BuiltInRole = null, Uuid? DeliveryAttemptId = null,
    string? TokenKeyId = null, IReadOnlyList<Uuid>? GuestProgramIds = null,
    DateOnly? GuestExpiresOn = null, DateTimeOffset InvitedAt = default,
    string? InvitedByDisplay = null) : DomainEvent;
