using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     An invitation was created. The fact is the native acceptance of the invitation's logical id on the invitation
///     stream and freezes the exact recipient, purpose, scopes and expiry. The bearer token never enters the fact,
///     only its hash and the key that derives it. <paramref name="MailboxGeneration" /> is the generation of the
///     mailbox claim the invitation was published under; only that generation may release it.
///     <paramref name="GuestProgramIdsManifest" /> references the complete ordered program scope.
/// </summary>
[Discriminator("bdgrz.tenant-member.invited", 1)]
public sealed record TenantMemberInvited(Uuid TenantId, Uuid InvitationId, string EmailAddress, string Affiliation,
    bool Administrator, string TokenHash, DateTimeOffset ExpiresAt, Uuid InvitedBy, string? BuiltInRole,
    string? TokenKeyId, ImmutableContentRef? GuestProgramIdsManifest, DateOnly? GuestExpiresOn,
    DateTimeOffset InvitedAt, string? InvitedByDisplay, long MailboxGeneration, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
