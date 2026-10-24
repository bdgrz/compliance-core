using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using System.Security.Cryptography;
using System.Text.Json;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class Member : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _selectedEpisodeId;
    Uuid _userId;
    bool _isRegistered;
    string? _affiliation;
    DateOnly? _membershipExpiresOn;
    Uuid _membershipEpisodeId;
    bool _isSuspended;
    bool _isDeprovisioned;
    bool _isDeprovisionCleanupComplete;
    Uuid _deprovisionedByMemberId;
    string? _deprovisionedByDisplay;
    DateTimeOffset? _deprovisionedAt;
    string? _deprovisionReason;
    MemberDeprovisioned? _currentTermination;
    MemberDeprovisioned? _selectedTermination;
    bool _selectedCleanupCompleted;

    /// <summary>Acceptance of the logical registration this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    public bool IsRegistered => _isRegistered;
    public string? Affiliation => _affiliation;
    public DateOnly? MembershipExpiresOn => _membershipExpiresOn;
    public Uuid UserId => _userId;
    public Uuid MembershipEpisodeId => _membershipEpisodeId;
    public bool IsSuspended => _isSuspended;
    public bool IsDeprovisioned => _isDeprovisioned;
    public bool IsDeprovisionCleanupComplete => _isDeprovisionCleanupComplete;
    public Uuid DeprovisionedByMemberId => _deprovisionedByMemberId;
    public string? DeprovisionedByDisplay => _deprovisionedByDisplay;
    public DateTimeOffset? DeprovisionedAt => _deprovisionedAt;
    public string? DeprovisionReason => _deprovisionReason;
    public bool SelectedCleanupCompleted => _selectedCleanupCompleted;
    public MembershipTerminationProof? TerminationProof => _selectedTermination is { } fact
        ? new MembershipTerminationProof(_tenantId, Id, fact.UserId, fact.MembershipEpisodeId,
            fact.Metadata.EventId, fact.Metadata.AggregateVersion, fact.Metadata.AggregateVersion - 1,
            Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(fact,
                ComplianceCoreJsonContext.Default.MemberDeprovisioned))),
            fact.DeprovisionedByMemberId, fact.DeprovisionedByDisplay)
        : null;

    public bool IsAccessEligibleOn(DateOnly utcDate) => IsRegistered && !_isSuspended &&
        !_isDeprovisioned && (_membershipExpiresOn is not { } expiresOn || utcDate <= expiresOn);

    public Member(Uuid tenantId, Uuid userId, Uuid selectedEpisodeId = default, Uuid selectedCommandId = default)
        : this(tenantId, RbacIds.Member(tenantId, userId), userId, selectedEpisodeId, selectedCommandId)
    {
    }

    /// <summary>
    ///     Opens a member stream by member identity for read-only verification, such as checking
    ///     that a responsibility holder is still active. Commands require the user constructor.
    /// </summary>
    public static Member ForVerification(Uuid tenantId, Uuid memberId, Uuid selectedEpisodeId = default) =>
        new(tenantId, memberId, Uuid.Empty, selectedEpisodeId, default);

    Member(Uuid tenantId, Uuid memberId, Uuid userId, Uuid selectedEpisodeId, Uuid selectedCommandId)
        : base(memberId,
            new EventStreamAddress(tenantId.ToString(), "rbac-members", memberId.ToString()))
    {
        _tenantId = tenantId;
        _selectedEpisodeId = selectedEpisodeId;
        _userId = userId;
        Acceptance = new NativeAcceptance(selectedCommandId);
        On<MemberRegistered>(registered =>
        {
            Acceptance.Observe(registered);
            if (registered.CommandId == Uuid.Empty || registered.MembershipEpisodeId != registered.CommandId ||
                registered.Operation.LogicalId != registered.CommandId || registered.Operation.Owner != Stream)
                throw new InvalidOperationException(
                    "The member registration event must accept its own logical operation as the membership episode.");
            var hasFiniteExpiry = registered.MembershipExpiresOn is { } expiresOn &&
                                  expiresOn != DateOnly.MinValue && expiresOn != DateOnly.MaxValue;
            if (registered.Affiliation is not ("client_personnel" or "firm_staff" or "guest") ||
                registered.Affiliation == "guest" && !hasFiniteExpiry ||
                registered.Affiliation != "guest" && registered.MembershipExpiresOn is not null)
                throw new InvalidOperationException("The member registration event has invalid affiliation or expiry terms.");
            _isRegistered = true;
            _affiliation = registered.Affiliation;
            _membershipExpiresOn = registered.MembershipExpiresOn;
            _userId = registered.UserId;
            _membershipEpisodeId = registered.MembershipEpisodeId;
            _isSuspended = false;
            _isDeprovisioned = false;
            _isDeprovisionCleanupComplete = false;
            _currentTermination = null;
            if (_selectedEpisodeId == Uuid.Empty)
            {
                _selectedTermination = null;
                _selectedCleanupCompleted = false;
            }
        });
        On<MemberSuspended>(_ => _isSuspended = true);
        On<MemberReinstated>(_ => _isSuspended = false);
        On<MemberDeprovisioned>(deprovisioned =>
        {
            if (deprovisioned.TenantId != _tenantId || deprovisioned.MemberId != Id ||
                RbacIds.Member(_tenantId, deprovisioned.UserId) != Id ||
                deprovisioned.DeprovisionedByMemberId == Uuid.Empty ||
                string.IsNullOrWhiteSpace(deprovisioned.DeprovisionedByDisplay) ||
                string.IsNullOrWhiteSpace(deprovisioned.Reason) || deprovisioned.DeprovisionedAt == default ||
                deprovisioned.UserId != _userId || !_isRegistered ||
                deprovisioned.MembershipEpisodeId == Uuid.Empty ||
                deprovisioned.MembershipEpisodeId != _membershipEpisodeId)
                throw new InvalidOperationException("Membership retirement must identify its exact registered episode.");
            _isRegistered = false;
            _isSuspended = false;
            _isDeprovisioned = true;
            _isDeprovisionCleanupComplete = false;
            _deprovisionedByMemberId = deprovisioned.DeprovisionedByMemberId;
            _deprovisionedByDisplay = deprovisioned.DeprovisionedByDisplay;
            _deprovisionedAt = deprovisioned.DeprovisionedAt;
            _deprovisionReason = deprovisioned.Reason;
            _currentTermination = deprovisioned;
            if (_selectedEpisodeId == Uuid.Empty || _selectedEpisodeId == deprovisioned.MembershipEpisodeId)
                _selectedTermination = deprovisioned;
        });
        On<MemberDeprovisionCleanupCompleted>(completed =>
        {
            if (completed.TenantId != _tenantId || completed.MemberId != Id || !_isDeprovisioned ||
                _isDeprovisionCleanupComplete || completed.CompletedAt == default ||
                completed.MembershipEpisodeId != _membershipEpisodeId || _currentTermination is null ||
                completed.TerminationEventId != _currentTermination.Metadata.EventId ||
                completed.CommandId != MemberRetirementEffects.CompletionId(completed.TerminationEventId,
                    Id, completed.MembershipEpisodeId))
                throw new InvalidOperationException("Cleanup completion must prove the exact membership retirement.");
            _isDeprovisionCleanupComplete = true;
            if (_selectedTermination?.Metadata.EventId == completed.TerminationEventId)
                _selectedCleanupCompleted = true;
        });
    }

    /// <summary>
    ///     Checks the registration guards against hydrated state without raising anything; the first half of the
    ///     native acceptance pair.
    /// </summary>
    public Result PrepareRegister(string affiliation = "client_personnel", DateOnly? membershipExpiresOn = null)
    {
        EnsureUserIdentity();
        if (affiliation is not ("client_personnel" or "firm_staff" or "guest"))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Invalid membership affiliation."));
        var hasFiniteExpiry = membershipExpiresOn is { } endDate &&
                              endDate != DateOnly.MinValue && endDate != DateOnly.MaxValue;
        if (affiliation == "guest" && !hasFiniteExpiry ||
            affiliation != "guest" && membershipExpiresOn is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Guest membership requires a finite end date; other affiliations cannot have one."));
        if (_isDeprovisioned && !_isDeprovisionCleanupComplete)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Membership authority cleanup must finish before a new invitation can take effect."));
        if (_isRegistered)
            return Result.Failure(LiveEpisodeConflict);
        return Result.Success;
    }

    /// <summary>
    ///     The one live-episode conflict a new registration can meet. Dispatchers that register as a side effect of
    ///     another accepted fact compare against this instance to continue past an already-live member.
    /// </summary>
    public static RequestError LiveEpisodeConflict { get; } = new(RequestErrorKind.Conflict,
        "This user already has a live membership episode.", false);

    /// <summary>
    ///     Accepts the registration as the native fact of <paramref name="operation" />. The logical id of the
    ///     operation is the membership episode id.
    /// </summary>
    public Result Register(OperationProvenance operation, ImmutableContentRef firstResponse,
        string affiliation = "client_personnel", DateOnly? membershipExpiresOn = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A member registration requires its own logical operation."));
        var prepared = PrepareRegister(affiliation, membershipExpiresOn);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new MemberRegistered(_tenantId, Id, _userId, affiliation, operation.LogicalId,
            membershipExpiresOn, operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    public Result Suspend(Uuid actorMemberId, string actorDisplay, DateTimeOffset suspendedAt,
        string reason)
    {
        EnsureUserIdentity();
        if (!_isRegistered)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant member was not found."));
        if (_isSuspended)
            return Result.Success;
        if (actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A member identity is required for the suspension history."));
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a suspension reason of at most 1000 characters."));

        RaiseEvent(new MemberSuspended(_tenantId, Id, _userId, actorMemberId,
            actorDisplay.Trim(), suspendedAt, reason.Trim()));
        return Result.Success;
    }

    public Result Reinstate(Uuid actorMemberId, string actorDisplay, DateTimeOffset reinstatedAt)
    {
        EnsureUserIdentity();
        if (!_isRegistered)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant member was not found."));
        if (!_isSuspended)
            return Result.Success;
        if (actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A member identity is required for the reinstatement history."));

        RaiseEvent(new MemberReinstated(_tenantId, Id, _userId, _affiliation!, actorMemberId,
            actorDisplay.Trim(), reinstatedAt));
        return Result.Success;
    }

    public Result Deprovision(Uuid actorMemberId, string actorDisplay, DateTimeOffset deprovisionedAt,
        string reason)
    {
        EnsureUserIdentity();
        if (_isDeprovisioned)
            return Result.Success;
        if (!_isRegistered)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant member was not found."));
        if (actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A member identity is required for the deprovision history."));
        if (deprovisionedAt == default)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A deprovision timestamp is required."));
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a deprovision reason of at most 1000 characters."));

        if (_membershipEpisodeId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Membership retirement requires its registered episode."));
        RaiseEvent(new MemberDeprovisioned(_tenantId, Id, _userId, actorMemberId,
            actorDisplay.Trim(), deprovisionedAt, reason.Trim(), _membershipEpisodeId));
        return Result.Success;
    }

    public Result CompleteDeprovisionCleanup(MembershipTerminationProof proof, Uuid commandId,
        DateTimeOffset completedAt)
    {
        EnsureUserIdentity();
        if (proof is null || proof != TerminationProof || commandId != MemberRetirementEffects.CompletionId(proof.EventId,
                Id, proof.MembershipEpisodeId))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Cleanup requires the exact original termination proof and operation."));
        if (_selectedCleanupCompleted)
            return Result.Success;
        if (!_isDeprovisioned || _membershipEpisodeId != proof.MembershipEpisodeId ||
            _currentTermination?.Metadata.EventId != proof.EventId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Membership authority cleanup requires a deprovisioned member."));
        if (_isDeprovisionCleanupComplete)
            return Result.Success;
        if (completedAt == default)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A cleanup completion timestamp is required."));

        RaiseEvent(new MemberDeprovisionCleanupCompleted(_tenantId, Id, proof.MembershipEpisodeId,
            proof.EventId, commandId, completedAt));
        return Result.Success;
    }

    void EnsureUserIdentity()
    {
        if (_userId == Uuid.Empty)
            throw new InvalidOperationException(
                "A member opened for verification cannot record membership changes.");
    }
}
