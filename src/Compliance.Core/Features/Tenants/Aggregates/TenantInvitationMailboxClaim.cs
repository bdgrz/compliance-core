using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     The single pending invitation of one tenant mailbox, keyed by tenant and normalized email. The holding
///     invitation id and the generation are state: every release compares both, and the generation is never part of
///     the stream key, so an accepted or expired invitation never blocks a later one.
/// </summary>
public sealed class TenantInvitationMailboxClaim : Aggregate
{
    /// <summary>The release reason recorded when the invitation was accepted.</summary>
    public const string AcceptedReason = "accepted";

    /// <summary>The release reason recorded when a later invitation superseded an unusable holder.</summary>
    public const string SupersededReason = "superseded";

    readonly Uuid _tenantId;
    readonly string _emailAddress;
    Uuid _holder;
    long _holderGeneration;
    long _generation;
    DateTimeOffset _claimedAt;

    public TenantInvitationMailboxClaim(Uuid tenantId, string emailAddress)
        : this(tenantId, Normalize(emailAddress), IdFor(tenantId, Normalize(emailAddress)))
    {
    }

    TenantInvitationMailboxClaim(Uuid tenantId, string normalized, Uuid id)
        : base(id, new EventStreamAddress(tenantId.ToString(), "tenant-invitation-mailboxes", id.ToString()))
    {
        _tenantId = tenantId;
        _emailAddress = normalized;
        On<TenantInvitationMailboxClaimed>(claimed =>
        {
            if (claimed.TenantId != _tenantId || claimed.EmailAddress != _emailAddress ||
                claimed.InvitationId == Uuid.Empty || claimed.Generation != _generation + 1 ||
                _holder != Uuid.Empty)
                throw new InvalidOperationException("The mailbox claim does not follow the current mailbox state.");
            _holder = claimed.InvitationId;
            _holderGeneration = claimed.Generation;
            _generation = claimed.Generation;
            _claimedAt = claimed.ClaimedAt;
        });
        On<TenantInvitationMailboxReleased>(released =>
        {
            if (released.TenantId != _tenantId || released.EmailAddress != _emailAddress ||
                released.InvitationId != _holder || released.Generation != _holderGeneration)
                throw new InvalidOperationException("The mailbox release does not name the current holder.");
            _holder = Uuid.Empty;
            _holderGeneration = 0;
        });
    }

    /// <summary>The invitation currently holding the mailbox, or <see cref="Uuid.Empty" />.</summary>
    public Uuid Holder => _holder;

    /// <summary>The generation of the current holder.</summary>
    public long HolderGeneration => _holderGeneration;

    /// <summary>When the current holder claimed the mailbox.</summary>
    public DateTimeOffset ClaimedAt => _claimedAt;

    /// <summary>The identity of the claim of one tenant mailbox.</summary>
    public static Uuid IdFor(Uuid tenantId, string normalizedEmail) =>
        AggregateIdentity.Create("TenantInvitationMailboxClaim", tenantId, normalizedEmail);

    /// <summary>
    ///     Claims the mailbox for <paramref name="invitationId" /> and returns the claim generation. The holder
    ///     claiming again keeps its generation. Another holder is replaced only when the caller names exactly that
    ///     holder and generation as unusable, so a concurrent claim in between conflicts instead of overwriting.
    /// </summary>
    public Result<long> Claim(Uuid invitationId, DateTimeOffset now, Uuid supersededHolder = default,
        long supersededGeneration = 0)
    {
        if (invitationId == Uuid.Empty || now == default)
            return Result<long>.Failure(new RequestError(RequestErrorKind.Validation,
                "A mailbox claim requires an invitation and a time."));
        if (_holder == invitationId)
            return Result<long>.Success(_holderGeneration);
        if (_holder != Uuid.Empty)
        {
            if (supersededHolder != _holder || supersededGeneration != _holderGeneration)
                return Result<long>.Failure(PendingInvitationConflict);
            RaiseEvent(new TenantInvitationMailboxReleased(_tenantId, _emailAddress, _holder, _holderGeneration,
                SupersededReason, now));
        }

        var generation = _generation + 1;
        RaiseEvent(new TenantInvitationMailboxClaimed(_tenantId, _emailAddress, invitationId, generation, now));
        return Result<long>.Success(generation);
    }

    /// <summary>
    ///     Releases the mailbox when the named invitation and generation still hold it. Any other release is a
    ///     no-op, so a late callback of an old generation can never affect a newer invitation.
    /// </summary>
    public Result Release(Uuid invitationId, long generation, string reason, DateTimeOffset now)
    {
        if (invitationId == Uuid.Empty || generation < 1 || string.IsNullOrWhiteSpace(reason) || now == default)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A mailbox release requires the invitation, generation, reason and time."));
        if (_holder != invitationId || _holderGeneration != generation)
            return Result.Success;
        RaiseEvent(new TenantInvitationMailboxReleased(_tenantId, _emailAddress, invitationId, generation, reason, now));
        return Result.Success;
    }

    /// <summary>The conflict for a mailbox that already has a usable pending invitation.</summary>
    public static RequestError PendingInvitationConflict { get; } = new(RequestErrorKind.Conflict,
        "A pending invitation already exists for this email address.", false);

    static string Normalize(string emailAddress) =>
        EmailAddresses.TryNormalize(emailAddress, out var normalized)
            ? normalized
            : throw new ArgumentException("A valid email address is required.", nameof(emailAddress));
}
