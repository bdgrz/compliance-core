using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Aggregates;

/// <summary>An external identity provider association for a platform user.</summary>
public sealed class UserIdentity : Aggregate
{
    bool _emitting;
    readonly Uuid _selectedChallengeId;
    bool _selectedSettled;
    Uuid? _protectionChallengeId;
    RecoveryProtection? _protection;
    string? _provider;
    string? _identifier;
    bool _isRegistered;
    bool _isRevoked;
    Uuid _userId;
    Uuid _replacementIdentityId;
    string? _emailAddress;
    IdentityRegistrationProof? _registrationProof;
    UserIdentityRegistered? _registration;
    UserIdentityRevoked? _revocation;

    public UserIdentity(string provider, string identifier)
        : this(CreateIdentityId(provider, identifier), provider, identifier, Uuid.Empty, null, Uuid.Empty)
    {
    }

    /// <summary>Hydrates an identity from its stable identity ID, for session revocation checks.</summary>
    public UserIdentity(Uuid identityId)
        : this(identityId, null, null, Uuid.Empty, null, Uuid.Empty)
    {
    }

    /// <summary>Selects a historical challenge on the exact provider and subject slot.</summary>
    public UserIdentity(string provider, string identifier, Uuid selectedChallengeId)
        : this(CreateIdentityId(provider, identifier), provider, identifier, Uuid.Empty, null, selectedChallengeId)
    {
    }

    /// <summary>Hydrates an identity by id for one logical recovery operation selected by command id and kind.</summary>
    public UserIdentity(Uuid identityId, Uuid selectedCommandId, string? selectedCommandKind, Uuid selectedChallengeId)
        : this(identityId, null, null, selectedCommandId, selectedCommandKind, selectedChallengeId)
    {
    }

    /// <summary>Hydrates an identity by provider and subject for one logical recovery operation.</summary>
    public UserIdentity(string provider, string identifier, Uuid selectedCommandId, string? selectedCommandKind,
        Uuid selectedChallengeId)
        : this(CreateIdentityId(provider, identifier), provider, identifier, selectedCommandId, selectedCommandKind, selectedChallengeId)
    {
    }

    UserIdentity(Uuid identityId, string? provider, string? identifier, Uuid selectedCommandId,
        string? selectedCommandKind, Uuid selectedChallengeId)
        : base(identityId, new EventStreamAddress("bdgrz", "user-identities", identityId.ToString()))
    {
        _provider = provider;
        _identifier = identifier;
        _selectedChallengeId = selectedChallengeId;
        Acceptance = new NativeAcceptance(selectedCommandId, selectedCommandKind);
        On<UserIdentityRegistered>(Apply);
        On<UserIdentityRevoked>(Apply);
        On<RecoveryIdentityProtected>(Apply);
        On<RecoveryIdentityProtectionSettled>(Apply);
    }

    static Uuid IdentityNamespaceId { get; } =
        Uuid.Parse("17729a2d-d41d-5c56-953a-d3fe810cc8d2", CultureInfo.InvariantCulture);

    /// <summary>The conflict a registration meets while the slot is protected for another account's recovery.</summary>
    internal static RequestError ProtectedForRecovery { get; } = new(RequestErrorKind.Conflict,
        "The provider identity is being recovered for another account.", false);

    /// <summary>Acceptance of the recovery operation this hydration was selected for.</summary>
    internal NativeAcceptance Acceptance { get; }

    internal Uuid SelectedChallengeId => _selectedChallengeId;
    internal IdentityOwnerReplayCut ReplayCut { get; } = new();
    internal Uuid? ProtectionChallengeId => _protectionChallengeId;
    internal RecoveryProtection? Protection => _protection;

    /// <summary>Selects a historical challenge explicitly; no command hash is reversed into a challenge.</summary>
    public UserIdentity(Uuid identityId, Uuid selectedChallengeId)
        : this(identityId, null, null, Uuid.Empty, null, selectedChallengeId)
    {
    }

    static Uuid CreateIdentityId(string provider, string identifier) =>
        Uuid.CreateVersion5(IdentityNamespaceId, $"{provider}\n{identifier}");

    public static Uuid GetIdentityId(string provider, string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return CreateIdentityId(provider, identifier);
    }

    public bool IsRegistered => _isRegistered;

    public bool IsRevoked => _isRevoked;

    public Uuid UserId => _userId;

    /// <summary>The provider of the identity, once known from its provider and subject or its registration.</summary>
    internal string? Provider => _provider;

    /// <summary>The provider subject of the identity, once known.</summary>
    internal string? Identifier => _identifier;

    /// <summary>The identity the revocation replaced this one with; empty until revoked.</summary>
    internal Uuid ReplacementIdentityId => _replacementIdentityId;

    /// <summary>The registration fact, once the identity is registered.</summary>
    internal UserIdentityRegistered? RegistrationFact => _registration;

    /// <summary>The revocation fact, once the identity is revoked.</summary>
    internal UserIdentityRevoked? RevocationFact => _revocation;

    /// <summary>True while the slot is protected for the recovery challenge.</summary>
    public bool IsProtectedFor(Uuid challengeId) => _protectionChallengeId == challengeId;

    /// <summary>True once the protection of the challenge was settled, which fences any later protection for it.</summary>
    public bool IsSettledFor(Uuid challengeId)
    {
        if (challengeId == _selectedChallengeId && challengeId != Uuid.Empty)
            return _selectedSettled;
        if (CommittedStreamPosition == 0 && !_selectedSettled)
            return false;
        throw new InvalidOperationException("Historical settlement authority requires its explicitly selected challenge.");
    }

    public Result<AuthenticatedUserIdentity> Register(
        Uuid? userId,
        string? emailAddress)
    {
        if (_provider is null || _identifier is null)
        {
            return Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "An identity must be registered from its provider and subject."));
        }

        if (userId == Uuid.Empty)
        {
            return Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A user identity requires a user ID."));
        }

        if (_isRegistered)
        {
            return userId is not null && _userId != userId.Value
                ? Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The provider identity belongs to another user."))
                : Registered();
        }

        // A slot protected for a recovery can only be associated with that recovery's account.
        if (_protection is { } protection && userId != protection.AccountId)
            return Result<AuthenticatedUserIdentity>.Failure(ProtectedForRecovery);

        var registeredUserId = userId ?? Uuid.CreateVersion4();
        Emit(new UserIdentityRegistered(
            registeredUserId,
            _provider,
            _identifier,
            emailAddress));
        return Registered();
    }

    public Result<AuthenticatedUserIdentity> Authenticate()
    {
        if (_isRevoked)
        {
            return Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.Unauthorized,
                "The provider identity has been revoked."));
        }

        if (!_isRegistered || _provider is null || _identifier is null)
        {
            return Result<AuthenticatedUserIdentity>.Failure(new RequestError(
                RequestErrorKind.NotFound,
                "The user identity is not registered."));
        }

        AuditEvent(new UserIdentityAuthenticated(_userId, _provider, _identifier));
        return Result<AuthenticatedUserIdentity>.Success(
            new AuthenticatedUserIdentity(Id, _userId, _emailAddress));
    }

    public Result Revoke(Uuid replacementIdentityId, DateTimeOffset revokedAt)
    {
        var prepared = PrepareRevoke(replacementIdentityId);
        if (!prepared.IsSuccess || _isRevoked)
            return prepared;
        // A slot held by a recovery can only be revoked by that recovery's own receiver.
        if (_protection is not null)
            return Result.Failure(ProtectedForRecovery);

        Emit(new UserIdentityRevoked(Id, _userId, replacementIdentityId, revokedAt));
        return Result.Success;
    }

    public Result<AuthenticatedUserIdentity> ObserveAuthenticatedProfile(string? displayName,
        DateTimeOffset observedAt)
    {
        if (_isRevoked)
            return Result<AuthenticatedUserIdentity>.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "The provider identity has been revoked."));
        if (!_isRegistered)
            return Result<AuthenticatedUserIdentity>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The user identity is not registered."));
        if (observedAt == default)
            return Result<AuthenticatedUserIdentity>.Failure(new RequestError(RequestErrorKind.Validation,
                "A profile observation time is required."));
        var normalized = displayName?.Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > 200 || normalized.Any(char.IsControl))
            normalized = null;
        // Admission linearizes at this authoritative read. A later revocation does not invalidate
        // this historical observation, and this audit can never restore identity authority.
        AuditEvent(new UserIdentityProfileObserved(_userId, normalized, observedAt,
            _registrationProof ?? throw new InvalidOperationException("Registration provenance is required.")));
        return Registered();
    }

    /// <summary>
    ///     What protecting this slot for a recovery decides: it may proceed, must wait for another recovery that holds
    ///     the slot, is fenced because the challenge was already settled, or conflicts definitively with the
    ///     authoritative association.
    /// </summary>
    public ProtectionDecision DecideProtection(Uuid challengeId, Uuid accountId, RecoveryIdentityRole role,
        Uuid replacementIdentityId)
    {
        if (IsSettledFor(challengeId))
            return new ProtectionDecision(ProtectionOutcome.Fenced, "The recovery protection was already settled.");
        if (role == RecoveryIdentityRole.Old)
        {
            if (!_isRegistered || _userId != accountId)
                return new ProtectionDecision(ProtectionOutcome.Conflict,
                    "The old identity is not linked to the recovering account.");
            if (_isRevoked && _replacementIdentityId != replacementIdentityId)
                return new ProtectionDecision(ProtectionOutcome.Conflict,
                    "The old identity was already revoked in favor of another identity.");
        }
        else if (_isRevoked)
        {
            return new ProtectionDecision(ProtectionOutcome.Conflict, "The replacement identity was revoked.");
        }
        else if (_isRegistered && _userId != accountId)
        {
            return new ProtectionDecision(ProtectionOutcome.Conflict,
                "The replacement identity is associated with another account.");
        }

        if (_protection is { } other && _protectionChallengeId != challengeId)
            return other.AccountId == accountId
                ? new ProtectionDecision(ProtectionOutcome.Wait, "Another recovery of this account holds the slot.")
                : new ProtectionDecision(ProtectionOutcome.Conflict,
                    "Another account's recovery holds the slot.");

        return new ProtectionDecision(ProtectionOutcome.Proceed, string.Empty);
    }

    /// <summary>Protects the slot for the recovery challenge as the native fact of <paramref name="operation" />.</summary>
    public Result Protect(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef? sourceProofs,
        Uuid challengeId, Uuid accountId, RecoveryIdentityRole role, Uuid replacementIdentityId, long generation)
    {
        var cut = RequireOperation(operation, challengeId);
        if (!cut.IsSuccess)
            return Result.Failure(cut.Error!);
        ArgumentNullException.ThrowIfNull(operation);
        var decision = DecideProtection(challengeId, accountId, role, replacementIdentityId);
        if (decision.Outcome != ProtectionOutcome.Proceed)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, decision.Reason, false));
        Emit(new RecoveryIdentityProtected(Stream, (long)Version, generation, intent, sourceProofs, operation,
            null, null, challengeId, accountId, role));
        return Result.Success;
    }

    /// <summary>Settles the protection of the recovery challenge, including a slot that was never protected.</summary>
    public Result Settle(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef? sourceProofs,
        Uuid challengeId, long generation)
    {
        var cut = RequireOperation(operation, challengeId);
        if (!cut.IsSuccess)
            return Result.Failure(cut.Error!);
        ArgumentNullException.ThrowIfNull(operation);
        Emit(new RecoveryIdentityProtectionSettled(Stream, (long)Version, generation, intent, sourceProofs,
            operation, null, null, challengeId));
        return Result.Success;
    }

    /// <summary>
    ///     Registers the recovered replacement for the account as a fact caused by the recovery. The caller proves the
    ///     recovery protects this slot.
    /// </summary>
    public Result RegisterRecovered(OperationProvenance operation, ImmutableContentRef intent, Uuid challengeId,
        Uuid accountId, string? emailAddress)
    {
        var cut = RequireOperation(operation, challengeId);
        if (!cut.IsSuccess)
            return Result.Failure(cut.Error!);
        ArgumentNullException.ThrowIfNull(operation);
        if (_provider is null || _identifier is null)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An identity must be registered from its provider and subject."));
        if (_isRegistered)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The provider identity is already registered.", false));
        if (_protectionChallengeId != challengeId || _protection is not { } protection || protection.AccountId != accountId ||
            protection.Role != RecoveryIdentityRole.Replacement)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The replacement slot is not protected for this recovery.", false));
        Emit(new UserIdentityRegistered(accountId, _provider, _identifier, emailAddress, operation, challengeId,
            intent));
        return Result.Success;
    }

    /// <summary>Checks that this identity may be revoked in favor of <paramref name="replacementIdentityId" />.</summary>
    public Result PrepareRevoke(Uuid replacementIdentityId)
    {
        if (!_isRegistered)
        {
            return Result.Failure(new RequestError(
                RequestErrorKind.NotFound,
                "The provider identity is not registered."));
        }

        if (replacementIdentityId == Uuid.Empty || replacementIdentityId == Id)
        {
            return Result.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A provider identity must be replaced by a different registered identity."));
        }

        if (_isRevoked)
        {
            return _replacementIdentityId == replacementIdentityId
                ? Result.Success
                : Result.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The provider identity has already been replaced."));
        }

        return Result.Success;
    }

    /// <summary>Revokes the old identity to its replacement as a fact caused by the recovery.</summary>
    public Result RevokeRecovered(OperationProvenance operation, ImmutableContentRef intent, Uuid challengeId,
        Uuid replacementIdentityId, DateTimeOffset revokedAt)
    {
        var cut = RequireOperation(operation, challengeId);
        if (!cut.IsSuccess)
            return Result.Failure(cut.Error!);
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareRevoke(replacementIdentityId);
        if (!prepared.IsSuccess)
            return prepared;
        if (_isRevoked)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The provider identity was already revoked by another operation.", false));
        if (_protectionChallengeId != challengeId || _protection is not { } protection || protection.AccountId != _userId ||
            protection.Role != RecoveryIdentityRole.Old)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The old slot is not protected for this recovery.", false));
        Emit(new UserIdentityRevoked(Id, _userId, replacementIdentityId, revokedAt, operation, challengeId,
            intent));
        return Result.Success;
    }

    void Apply(UserIdentityRegistered registered)
    {
        if (registered.Operation is { } operation)
            Acceptance.Observe(registered, operation);
        if (_isRegistered || registered.UserId == Uuid.Empty ||
            (_provider is not null && !string.Equals(registered.Provider, _provider, StringComparison.Ordinal)) ||
            (_identifier is not null && !string.Equals(registered.Identifier, _identifier, StringComparison.Ordinal)) ||
            CreateIdentityId(registered.Provider, registered.Identifier) != Id)
        {
            throw new InvalidOperationException("The user identity stream does not match its provider identity.");
        }

        _isRegistered = true;
        _provider = registered.Provider;
        _identifier = registered.Identifier;
        _userId = registered.UserId;
        _emailAddress = registered.EmailAddress;
        _registration = registered;
        // A hydrated proof is bound by the full reader to its actual physical record. A fresh first
        // registration has no predecessor records; it is the only unsaved case used by aggregate scenarios.
        _registrationProof = Version == 0 && CommittedStreamPosition == 0
            ? RegistrationProof(registered, 0)
            : null;
    }

    void Apply(UserIdentityRevoked revoked)
    {
        if (revoked.Operation is { } operation)
            Acceptance.Observe(revoked, operation);
        if (!_isRegistered || _isRevoked || revoked.UserIdentityId != Id || revoked.UserId != _userId ||
            revoked.ReplacementIdentityId == Id)
        {
            throw new InvalidOperationException("The identity revocation does not match its registered identity.");
        }

        _isRevoked = true;
        _replacementIdentityId = revoked.ReplacementIdentityId;
        _revocation = revoked;
    }

    void Apply(RecoveryIdentityProtected protectedFact)
    {
        Acceptance.Observe(protectedFact);
        if (protectedFact.Owner != Stream || protectedFact.ChallengeId == Uuid.Empty ||
            protectedFact.AccountId == Uuid.Empty || _protection is not null ||
            protectedFact.ChallengeId == _selectedChallengeId && _selectedSettled)
            throw new InvalidOperationException("The recovery protection does not match its identity slot.");
        (_protectionChallengeId, _protection) = (protectedFact.ChallengeId,
            new RecoveryProtection(protectedFact.AccountId, protectedFact.Role));
    }

    void Apply(RecoveryIdentityProtectionSettled settled)
    {
        Acceptance.Observe(settled);
        if (settled.Owner != Stream || settled.ChallengeId == Uuid.Empty ||
            settled.ChallengeId == _selectedChallengeId && _selectedSettled)
            throw new InvalidOperationException("The recovery protection settlement does not match its identity slot.");
        if (settled.ChallengeId == _selectedChallengeId)
            _selectedSettled = true;
        if (_protectionChallengeId == settled.ChallengeId)
            (_protectionChallengeId, _protection) = (null, null);
    }

    Result RequireOperation(OperationProvenance operation, Uuid challengeId)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var cut = ReplayCut.Require(this);
        if (!cut.IsSuccess || CommittedStreamPosition == 0)
            return cut;
        return _selectedChallengeId == challengeId && Acceptance.SelectedLogicalId == operation.LogicalId &&
               Acceptance.SelectedCommandKind == operation.CommandKind && Acceptance.SelectedFact is null
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The recovery append requires its exact selected challenge and native command cut.", true));
    }

    void Emit(DomainEvent fact)
    {
        _emitting = true;
        try
        {
            RaiseEvent(fact);
        }
        finally
        {
            _emitting = false;
        }
    }

    protected override void Apply(DomainEvent ev)
    {
        base.Apply(ev);
        ReplayCut.Observe(ev, _emitting);
    }

    internal void BindRegistrationProof(UserIdentityRegistered registered, ulong physicalPosition)
    {
        if (_registration != registered || _registration.Metadata != registered.Metadata)
            throw new InvalidOperationException("The registration proof does not match the compact identity authority.");
        _registrationProof = RegistrationProof(registered, physicalPosition);
    }

    static IdentityRegistrationProof RegistrationProof(UserIdentityRegistered registered, ulong position) =>
        new(registered.Metadata.AggregateId, registered.Metadata.EventId, registered.Metadata.AggregateVersion, position,
            Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(registered,
                ComplianceCoreJsonContext.Default.UserIdentityRegistered))));

    Result<AuthenticatedUserIdentity> Registered() =>
        Result<AuthenticatedUserIdentity>.Success(new AuthenticatedUserIdentity(Id, _userId, _emailAddress));

    internal sealed record RecoveryProtection(Uuid AccountId, RecoveryIdentityRole Role);

    /// <summary>The outcomes of deciding whether a slot can be protected for a recovery.</summary>
    public enum ProtectionOutcome
    {
        /// <summary>The slot may be protected now.</summary>
        Proceed,

        /// <summary>Another recovery of the same account holds the slot; it settles before this one can protect.</summary>
        Wait,

        /// <summary>The challenge was settled on this slot, so a delayed protection is a terminal no-op.</summary>
        Fenced,

        /// <summary>The authoritative association conflicts, which rejects the preparation.</summary>
        Conflict,
    }

    /// <summary>A protection decision and the reason behind it.</summary>
    public sealed record ProtectionDecision(ProtectionOutcome Outcome, string Reason);
}
