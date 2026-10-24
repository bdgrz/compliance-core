using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Owns the one-user reservation for a normalized email address.</summary>
public sealed class EmailAddress : Aggregate
{
    static readonly Uuid AddressNamespaceId =
        Uuid.Parse("2c581f32-1b42-5928-8bc1-e604d4a9cd58", CultureInfo.InvariantCulture);

    readonly Dictionary<Uuid, RecoveryClaim> _claims = [];
    readonly HashSet<Uuid> _completedRecoveries = [];
    string? _address;
    Uuid? _owner;
    bool _isVerified;
    Uuid? _challengeId;
    string? _tokenHash;
    string _deliveryStatus = "not_issued";
    DateTimeOffset _expiresAt;
    Uuid? _recoveryChallengeId;
    string? _recoveryTokenHash;
    string? _recoveryTokenKeyId;
    DateTimeOffset _recoveryExpiresAt;
    string _recoveryDeliveryStatus = "not_issued";
    bool _recoveryCompleted;

    public EmailAddress(string address)
        : this(address, Uuid.Empty, null)
    {
    }

    /// <summary>
    ///     Hydrates the address for one logical operation: <paramref name="selectedLogicalId" /> and
    ///     <paramref name="selectedCommandKind" /> select the accepted fact that retry resolution compares against.
    /// </summary>
    public EmailAddress(string address, Uuid selectedLogicalId, string? selectedCommandKind)
        : this(CreateId(address), Normalize(address), selectedLogicalId, selectedCommandKind)
    {
    }

    /// <summary>Hydrates an address by its stable id; the normalized address is learned from its first fact.</summary>
    public EmailAddress(Uuid addressId, Uuid selectedLogicalId, string? selectedCommandKind)
        : this(addressId, null, selectedLogicalId, selectedCommandKind)
    {
    }

    EmailAddress(Uuid addressId, string? address, Uuid selectedLogicalId, string? selectedCommandKind)
        : base(addressId, new EventStreamAddress("bdgrz", "email-addresses", addressId.ToString()))
    {
        _address = address;
        Acceptance = new NativeAcceptance(selectedLogicalId, selectedCommandKind);
        On<EmailAddressReserved>(Apply);
        On<EmailChallengeIssued>(Apply);
        On<EmailChallengeDeliverySent>(Apply);
        On<EmailChallengeDeliveryFailed>(Apply);
        On<EmailAddressVerified>(Apply);
        On<IdentityRecoveryChallengeIssued>(Apply);
        On<IdentityRecoveryChallengeClaimed>(Apply);
        On<IdentityRecoveryChallengeCompleted>(Apply);
    }

    /// <summary>The conflict a recovery start meets while an unexpired challenge is outstanding.</summary>
    public static RequestError OutstandingRecoveryChallenge { get; } = new(RequestErrorKind.Conflict,
        "An identity recovery challenge is already outstanding.");

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The normalized address, once known.</summary>
    public string? Address => _address;

    public bool IsVerified => _isVerified;
    public Uuid? Owner => _owner;
    public Uuid? CurrentChallengeId => _challengeId;
    public DateTimeOffset ChallengeExpiresAt => _expiresAt;
    public string DeliveryStatus => _deliveryStatus;
    public Uuid? CurrentRecoveryChallengeId => _recoveryChallengeId;
    public DateTimeOffset RecoveryChallengeExpiresAt => _recoveryExpiresAt;
    public string RecoveryDeliveryStatus => _recoveryDeliveryStatus;
    public string? RecoveryTokenKeyId => _recoveryTokenKeyId;
    public Uuid? RecoveryOldIdentityId => CurrentClaim?.OldIdentityId;
    public Uuid? RecoveryReplacementIdentityId => CurrentClaim?.ReplacementIdentityId;
    public bool IsRecoveryCompleted => _recoveryCompleted;

    RecoveryClaim? CurrentClaim => _recoveryChallengeId is { } current ? _claims.GetValueOrDefault(current) : null;

    string Known => _address ?? throw new InvalidOperationException("The email address is not known yet.");

    /// <summary>The claim recorded for a recovery challenge of this address, if it was claimed.</summary>
    public RecoveryClaim? ClaimOf(Uuid challengeId) => _claims.GetValueOrDefault(challengeId);

    /// <summary>True when the original email challenge of the recovery was completed.</summary>
    public bool IsRecoveryChallengeCompleted(Uuid challengeId) => _completedRecoveries.Contains(challengeId);

    /// <summary>True while an unexpired recovery challenge is outstanding and may not be issued again.</summary>
    public bool HasOutstandingRecoveryChallenge(DateTimeOffset now) =>
        _recoveryChallengeId is not null && !_recoveryCompleted && now < _recoveryExpiresAt &&
        _recoveryDeliveryStatus != "failed";

    public EmailChallengeStatusView GetChallengeStatus(DateTimeOffset now) =>
        new(_isVerified ? "verified" : _challengeId is null ? "not_issued" :
                now >= _expiresAt ? "expired" : _deliveryStatus,
            _challengeId is null ? null : _expiresAt);

    public Result IssueChallenge(Uuid userId, Uuid challengeId, string tokenHash, DateTimeOffset expiresAt,
        DateTimeOffset now, string? tokenKeyId = null)
    {
        if (_owner != userId)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The email address is not owned by this user."));
        if (_challengeId == challengeId)
            return Result.Success;
        if (_isVerified)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The email address is already verified."));
        if (challengeId == Uuid.Empty || tokenHash.Length != 64 || expiresAt <= now)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A valid future challenge is required."));

        RaiseEvent(new EmailChallengeIssued(userId, Known, challengeId, tokenHash, expiresAt, tokenKeyId));
        return Result.Success;
    }

    /// <summary>Checks a recovery challenge issuance against hydrated state without raising anything.</summary>
    public Result PrepareIssueRecoveryChallenge(Uuid userId, Uuid challengeId, string tokenHash,
        DateTimeOffset expiresAt, DateTimeOffset now, string tokenKeyId)
    {
        if (_owner != userId || !_isVerified)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Identity recovery requires an email address verified for this user."));
        if (challengeId == Uuid.Empty || tokenHash.Length != 64 || expiresAt <= now ||
            string.IsNullOrWhiteSpace(tokenKeyId))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A valid future recovery challenge is required."));
        if (challengeId == _challengeId || _claims.ContainsKey(challengeId))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The challenge identity is already used on this address."));
        return HasOutstandingRecoveryChallenge(now)
            ? Result.Failure(OutstandingRecoveryChallenge)
            : Result.Success;
    }

    /// <summary>Accepts the recovery challenge issuance as the native fact of <paramref name="operation" />.</summary>
    public Result IssueRecoveryChallenge(OperationProvenance operation, ImmutableContentRef firstResponse,
        Uuid userId, Uuid challengeId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now,
        string tokenKeyId)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareIssueRecoveryChallenge(userId, challengeId, tokenHash, expiresAt, now, tokenKeyId);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new IdentityRecoveryChallengeIssued(userId, Known, challengeId, tokenHash, expiresAt, tokenKeyId,
            operation, firstResponse, null));
        return Result.Success;
    }

    /// <summary>
    ///     Checks a claim of the recovery challenge against hydrated state without raising anything: the challenge must
    ///     be the current one of the verified owner, carry a valid unexpired token and not be claimed yet.
    /// </summary>
    public Result PrepareClaimIdentityRecovery(Uuid userId, Uuid challengeId, string token, Uuid oldIdentityId,
        Uuid replacementIdentityId, DateTimeOffset now)
    {
        if (oldIdentityId == Uuid.Empty || replacementIdentityId == Uuid.Empty ||
            oldIdentityId == replacementIdentityId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Different old and replacement identities are required."));
        if (_owner != userId || !_isVerified || _recoveryChallengeId != challengeId)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "No identity recovery challenge is available."));
        if (_claims.ContainsKey(challengeId))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The recovery challenge is already bound to another identity replacement."));
        var proof = ValidateIdentityRecoveryChallenge(userId, challengeId, token, now);
        return proof.IsSuccess ? Result.Success : Result.Failure(proof.Error);
    }

    /// <summary>Accepts the claim as the native fact of <paramref name="operation" />.</summary>
    public Result ClaimIdentityRecovery(OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef firstResponse, ActorReference originalActor, ImmutableContentRef emailProof,
        ImmutableContentRef replacementProof, DateTimeOffset acceptedAt, Uuid userId, Uuid challengeId, string token,
        Uuid oldIdentityId, Uuid replacementIdentityId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareClaimIdentityRecovery(userId, challengeId, token, oldIdentityId, replacementIdentityId, now);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new IdentityRecoveryChallengeClaimed(userId, Known, challengeId, oldIdentityId,
            replacementIdentityId, operation, originalActor, emailProof, replacementProof, acceptedAt, intent,
            firstResponse, null));
        return Result.Success;
    }

    public Result<Uuid> ValidateIdentityRecoveryChallenge(Uuid userId, Uuid challengeId,
        string token, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 64 ||
            token.Any(character => !Uri.IsHexDigit(character)))
            return Result<Uuid>.Failure(new RequestError(RequestErrorKind.Validation,
                "The identity recovery challenge is invalid."));
        if (_owner != userId || !_isVerified || _recoveryChallengeId != challengeId ||
            _recoveryTokenHash is null)
            return Result<Uuid>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "No identity recovery challenge is available."));
        if (now >= _recoveryExpiresAt)
            return Result<Uuid>.Failure(new RequestError(RequestErrorKind.Validation,
                "The identity recovery challenge has expired."));

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var expectedHash = Convert.FromHexString(_recoveryTokenHash);
        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash)
            ? Result<Uuid>.Success(userId)
            : Result<Uuid>.Failure(new RequestError(RequestErrorKind.Validation,
                "The identity recovery challenge is invalid."));
    }

    /// <summary>
    ///     Checks the completion of a claimed recovery challenge without raising anything. The claim must name this
    ///     exact replacement, and a challenge completes at most once.
    /// </summary>
    public Result PrepareCompleteIdentityRecovery(Uuid userId, Uuid challengeId, Uuid oldIdentityId,
        Uuid replacementIdentityId)
    {
        if (_owner != userId || !_claims.TryGetValue(challengeId, out var claim) ||
            claim.OldIdentityId != oldIdentityId || claim.ReplacementIdentityId != replacementIdentityId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The identity recovery challenge is not claimed for this replacement.", false));
        return _completedRecoveries.Contains(challengeId)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The identity recovery challenge is already completed.", false))
            : Result.Success;
    }

    /// <summary>
    ///     Accepts the completion as the native fact of <paramref name="operation" />. Only the completed challenge
    ///     is marked; a newer challenge slot is never cleared.
    /// </summary>
    public Result CompleteIdentityRecovery(OperationProvenance operation, ImmutableContentRef intent, Uuid userId,
        Uuid challengeId, Uuid oldIdentityId, Uuid replacementIdentityId, DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareCompleteIdentityRecovery(userId, challengeId, oldIdentityId, replacementIdentityId);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new IdentityRecoveryChallengeCompleted(userId, Known, challengeId, oldIdentityId,
            replacementIdentityId, completedAt, operation, intent, null, null));
        return Result.Success;
    }

    public Result RecordDeliverySent(Uuid challengeId, DateTimeOffset sentAt)
    {
        if (_recoveryChallengeId == challengeId)
            return RecordRecoveryDeliverySent(challengeId, sentAt);
        if (_challengeId != challengeId || _isVerified || _deliveryStatus == "delivered")
            return Result.Success;
        if (_deliveryStatus == "failed")
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The failed email challenge must be reissued."));
        RaiseEvent(new EmailChallengeDeliverySent(challengeId, sentAt));
        return Result.Success;
    }

    public Result RecordDeliveryFailure(Uuid challengeId, string failureCode, DateTimeOffset failedAt)
    {
        if (_recoveryChallengeId == challengeId)
            return RecordRecoveryDeliveryFailure(challengeId, failureCode, failedAt);
        if (_challengeId != challengeId || _isVerified || _deliveryStatus is "failed" or "delivered")
            return Result.Success;
        if (failureCode is not ("delivery_failed" or "key_unavailable"))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An email delivery failure code is required."));
        RaiseEvent(new EmailChallengeDeliveryFailed(challengeId, failureCode, failedAt));
        return Result.Success;
    }

    public Result RecordRecoveryDeliverySent(Uuid challengeId, DateTimeOffset sentAt)
    {
        if (_recoveryChallengeId != challengeId || _recoveryDeliveryStatus == "delivered")
            return Result.Success;
        if (_recoveryDeliveryStatus == "failed")
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The failed identity recovery challenge must be reissued."));
        RaiseEvent(new EmailChallengeDeliverySent(challengeId, sentAt));
        return Result.Success;
    }

    public Result RecordRecoveryDeliveryFailure(Uuid challengeId, string failureCode, DateTimeOffset failedAt)
    {
        if (_recoveryChallengeId != challengeId || _recoveryDeliveryStatus is "failed" or "delivered")
            return Result.Success;
        if (failureCode is not ("delivery_failed" or "key_unavailable"))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An email delivery failure code is required."));
        RaiseEvent(new EmailChallengeDeliveryFailed(challengeId, failureCode, failedAt));
        return Result.Success;
    }

    public Result CompleteChallenge(Uuid userId, string token, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A verification challenge is required."));
        if (_owner != userId || _challengeId is null || _tokenHash is null)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "No verification challenge is available."));
        if (now >= _expiresAt)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The verification challenge has expired."));

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var expectedHash = Convert.FromHexString(_tokenHash);
        if (!CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The verification challenge is invalid."));

        if (!_isVerified)
            RaiseEvent(new EmailAddressVerified(userId, Known, _challengeId.Value));
        return Result.Success;
    }

    /// <summary>
    ///     Reserves the address for the user. A non-negative <paramref name="expectedRevision" /> must equal the
    ///     number of facts the address holds; a negative revision is unconstrained and reserved for system callers.
    /// </summary>
    public Result Reserve(Uuid userId, long expectedRevision = -1)
    {
        if (userId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "An email owner is required."));
        if (expectedRevision >= 0 && (ulong)expectedRevision != Version)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The email address changed since it was read."));

        if (_owner is null)
            RaiseEvent(new EmailAddressReserved(userId, Known));
        else if (_owner != userId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The email address belongs to another user."));

        return Result.Success;
    }

    void Learn(string address)
    {
        if (_address is not null)
        {
            if (!string.Equals(address, _address, StringComparison.Ordinal))
                throw new InvalidOperationException("The email fact does not match its address.");
            return;
        }

        if (!EmailAddresses.TryNormalize(address, out var normalized) ||
            !string.Equals(normalized, address, StringComparison.Ordinal) || CreateId(normalized) != Id)
            throw new InvalidOperationException("The email fact does not match its address stream.");
        _address = normalized;
    }

    void Apply(EmailAddressReserved reserved)
    {
        Learn(reserved.EmailAddress);
        if (_owner is not null || reserved.UserId == Uuid.Empty)
            throw new InvalidOperationException("The email reservation does not match its address.");
        _owner = reserved.UserId;
    }

    void Apply(EmailChallengeIssued issued)
    {
        Learn(issued.EmailAddress);
        if (_owner != issued.UserId || _isVerified)
            throw new InvalidOperationException("The email challenge does not match its owner and address.");
        _challengeId = issued.ChallengeId;
        _tokenHash = issued.TokenHash;
        _expiresAt = issued.ExpiresAt;
        _deliveryStatus = "pending";
    }

    void Apply(EmailChallengeDeliverySent sent)
    {
        if (_challengeId == sent.ChallengeId)
            _deliveryStatus = "delivered";
        else if (_recoveryChallengeId == sent.ChallengeId)
            _recoveryDeliveryStatus = "delivered";
        else
            throw new InvalidOperationException("The email delivery outcome is stale.");
    }

    void Apply(EmailChallengeDeliveryFailed failed)
    {
        if (_challengeId == failed.ChallengeId)
            _deliveryStatus = "failed";
        else if (_recoveryChallengeId == failed.ChallengeId)
            _recoveryDeliveryStatus = "failed";
        else
            throw new InvalidOperationException("The email delivery outcome is stale.");
    }

    void Apply(EmailAddressVerified verified)
    {
        Learn(verified.EmailAddress);
        if (_owner != verified.UserId || _challengeId != verified.ChallengeId)
            throw new InvalidOperationException("The email verification does not match its owner and address.");
        _isVerified = true;
    }

    void Apply(IdentityRecoveryChallengeIssued issued)
    {
        Acceptance.Observe(issued);
        Learn(issued.EmailAddress);
        if (_owner != issued.UserId || !_isVerified || issued.ChallengeId == Uuid.Empty ||
            issued.TokenHash.Length != 64 || issued.ExpiresAt <= DateTimeOffset.MinValue ||
            string.IsNullOrWhiteSpace(issued.TokenKeyId))
            throw new InvalidOperationException("The identity recovery challenge does not match its verified address.");

        _recoveryChallengeId = issued.ChallengeId;
        _recoveryTokenHash = issued.TokenHash;
        _recoveryTokenKeyId = issued.TokenKeyId;
        _recoveryExpiresAt = issued.ExpiresAt;
        _recoveryDeliveryStatus = "pending";
        _recoveryCompleted = false;
    }

    void Apply(IdentityRecoveryChallengeClaimed claimed)
    {
        Acceptance.Observe(claimed);
        Learn(claimed.EmailAddress);
        if (_owner != claimed.UserId || !_isVerified || _recoveryChallengeId != claimed.ChallengeId ||
            _claims.ContainsKey(claimed.ChallengeId) ||
            claimed.OldIdentityId == Uuid.Empty || claimed.ReplacementIdentityId == Uuid.Empty ||
            claimed.OldIdentityId == claimed.ReplacementIdentityId)
            throw new InvalidOperationException("The identity recovery claim does not match its challenge.");

        _claims[claimed.ChallengeId] = new RecoveryClaim(claimed.OldIdentityId, claimed.ReplacementIdentityId, claimed);
    }

    void Apply(IdentityRecoveryChallengeCompleted completed)
    {
        Acceptance.Observe(completed);
        Learn(completed.EmailAddress);
        if (_owner != completed.UserId || !_claims.TryGetValue(completed.ChallengeId, out var claim) ||
            claim.OldIdentityId != completed.OldIdentityId ||
            claim.ReplacementIdentityId != completed.ReplacementIdentityId ||
            !_completedRecoveries.Add(completed.ChallengeId))
            throw new InvalidOperationException("The identity recovery completion does not match its claim.");

        // Only the completed challenge is marked; a newer challenge slot is left exactly as it is.
        if (_recoveryChallengeId == completed.ChallengeId)
            _recoveryCompleted = true;
    }

    static Uuid CreateId(string address) => Uuid.CreateVersion5(AddressNamespaceId, Normalize(address));

    static string Normalize(string address) => EmailAddresses.TryNormalize(address, out var normalized)
        ? normalized
        : throw new ArgumentException("A valid email address is required.", nameof(address));

    /// <summary>The old and replacement identities a claimed recovery challenge froze, and the fact that froze them.</summary>
    public sealed record RecoveryClaim(Uuid OldIdentityId, Uuid ReplacementIdentityId,
        IdentityRecoveryChallengeClaimed Fact);
}
