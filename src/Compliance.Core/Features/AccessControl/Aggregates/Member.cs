using Cntryl.Portia;
using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Aggregates;

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
    Uuid? _linkedPersonId;
    Uuid? _pendingPersonId;
    long _personLinkGeneration;
    MemberPersonLinkAdmitted? _personLinkAdmission;

    /// <summary>Acceptance of the logical registration this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    public Uuid? LinkedPersonId => _linkedPersonId;
    public Uuid? PendingPersonId => _pendingPersonId;
    public long PersonLinkGeneration => _personLinkGeneration;
    public MemberPersonLinkAdmitted? PersonLinkAdmission => _personLinkAdmission;
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
        On<MemberPersonLinkAdmitted>(fact =>
        {
            RequireMemberIdentity(fact.TenantId, fact.MemberId, fact.UserId);
            RequireOwnOperation(fact.Operation);
            if (fact.PersonId == Uuid.Empty || fact.LinkId != fact.Operation.LogicalId ||
                fact.Generation != _personLinkGeneration + 1 || _linkedPersonId is not null || _pendingPersonId is not null)
                throw new MemberHistoryIntegrityException("A member person admission must advance one unbound generation.");
            _pendingPersonId = fact.PersonId;
            _personLinkGeneration = fact.Generation;
            _personLinkAdmission = fact;
            Acceptance.Observe(fact);
        });
        On<MemberPersonLinkRefused>(fact =>
        {
            RequireMemberIdentity(fact.TenantId, fact.MemberId, fact.UserId);
            RequireOwnOperation(fact.Operation);
            if (fact.PersonId == Uuid.Empty || fact.LinkId != fact.Operation.LogicalId || fact.Generation != _personLinkGeneration ||
                string.IsNullOrWhiteSpace(fact.Reason))
                throw new MemberHistoryIntegrityException("A refused person admission cannot alter the member binding.");
            Acceptance.Observe(fact);
        });
        On<MemberPersonLinkConfirmed>(fact =>
        {
            RequirePersonConclusion(fact.TenantId, fact.MemberId, fact.UserId, fact.PersonId, fact.Link);
            RequireOwnOperation(fact.Operation);
            if (_pendingPersonId != fact.PersonId || _linkedPersonId is not null)
                throw new MemberHistoryIntegrityException("A person link confirmation needs the exact pending admission.");
            _pendingPersonId = null;
            _linkedPersonId = fact.PersonId;
            Acceptance.Observe(fact);
        });
        On<MemberPersonLinkAdmissionCancelled>(fact =>
        {
            RequirePersonConclusion(fact.TenantId, fact.MemberId, fact.UserId, fact.PersonId, fact.Link);
            RequireOwnOperation(fact.Operation);
            if (_pendingPersonId != fact.PersonId || _linkedPersonId is not null || fact.Generation != _personLinkGeneration + 1)
                throw new MemberHistoryIntegrityException("A cancelled admission must release its exact pending generation.");
            _pendingPersonId = null;
            _personLinkGeneration = fact.Generation;
            _personLinkAdmission = null;
            Acceptance.Observe(fact);
        });
        On<MemberPersonLinkReleased>(fact =>
        {
            RequirePersonConclusion(fact.TenantId, fact.MemberId, fact.UserId, fact.PersonId, fact.Link);
            RequireOwnOperation(fact.Operation);
            if ((_pendingPersonId ?? _linkedPersonId) != fact.PersonId || fact.Generation != _personLinkGeneration + 1)
                throw new MemberHistoryIntegrityException("A released link must release its exact admitted generation.");
            _pendingPersonId = null;
            _linkedPersonId = null;
            _personLinkGeneration = fact.Generation;
            _personLinkAdmission = null;
            Acceptance.Observe(fact);
        });
        On<MemberRegistered>(registered =>
        {
            if (_isRegistered || _isDeprovisioned && !_isDeprovisionCleanupComplete)
                throw new MemberHistoryIntegrityException("A new membership episode requires the previous episode's authority cleanup to be complete.");
            if (registered.TenantId != _tenantId || registered.MemberId != Id || registered.UserId == Uuid.Empty ||
                RbacIds.Member(_tenantId, registered.UserId) != Id || _userId != Uuid.Empty && registered.UserId != _userId)
                throw new MemberHistoryIntegrityException("The member registration must retain the exact tenant and user identity.");
            if (registered.CommandId == Uuid.Empty || registered.MembershipEpisodeId != registered.CommandId ||
                registered.Operation.LogicalId != registered.CommandId || registered.Operation.Owner != Stream)
                throw new MemberHistoryIntegrityException(
                    "The member registration event must accept its own logical operation as the membership episode.");
            var hasFiniteExpiry = registered.MembershipExpiresOn is { } expiresOn &&
                                  expiresOn != DateOnly.MinValue && expiresOn != DateOnly.MaxValue;
            if (registered.Affiliation is not ("client_personnel" or "firm_staff" or "guest") ||
                registered.Affiliation == "guest" && !hasFiniteExpiry ||
                registered.Affiliation != "guest" && registered.MembershipExpiresOn is not null)
                throw new MemberHistoryIntegrityException("The member registration event has invalid affiliation or expiry terms.");
            Acceptance.Observe(registered);
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
        On<MemberSuspended>(suspended =>
        {
            RequireOwnOperation(suspended.Operation);
            RequireMemberIdentity(suspended.TenantId, suspended.MemberId, suspended.UserId);
            Acceptance.Observe(suspended);
            _isSuspended = true;
        });
        On<MemberReinstated>(reinstated =>
        {
            RequireOwnOperation(reinstated.Operation);
            RequireMemberIdentity(reinstated.TenantId, reinstated.MemberId, reinstated.UserId);
            Acceptance.Observe(reinstated);
            _isSuspended = false;
        });
        On<MemberDeprovisioned>(deprovisioned =>
        {
            Acceptance.Observe(deprovisioned);
            RequireOwnOperation(deprovisioned.Operation);
            if (deprovisioned.TenantId != _tenantId || deprovisioned.MemberId != Id ||
                RbacIds.Member(_tenantId, deprovisioned.UserId) != Id ||
                deprovisioned.DeprovisionedByMemberId == Uuid.Empty ||
                string.IsNullOrWhiteSpace(deprovisioned.DeprovisionedByDisplay) ||
                string.IsNullOrWhiteSpace(deprovisioned.Reason) || deprovisioned.DeprovisionedAt == default ||
                deprovisioned.UserId != _userId || !_isRegistered ||
                deprovisioned.MembershipEpisodeId == Uuid.Empty ||
                deprovisioned.MembershipEpisodeId != _membershipEpisodeId)
                throw new MemberHistoryIntegrityException("Membership retirement must identify its exact registered episode.");
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
                throw new MemberHistoryIntegrityException("Cleanup completion must prove the exact membership retirement.");
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

    /// <summary>
    ///     Checks a suspension against hydrated state without raising anything. A negative revision and an empty
    ///     episode are unconstrained and reserved for system callers. <paramref name="suspensionPending" /> is true when
    ///     the guard already admitted a suspension of this episode that is not applied yet.
    /// </summary>
    public Result PrepareSuspend(Uuid episodeId, long expectedRevision, string reason, bool suspensionPending = false)
    {
        EnsureUserIdentity();
        var current = CheckEpisode(episodeId, expectedRevision);
        if (!current.IsSuccess)
            return current;
        if (_isSuspended || suspensionPending)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The member is already suspended."));
        return string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000
            ? Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a suspension reason of at most 1000 characters."))
            : Result.Success;
    }

    /// <summary>Accepts the suspension as the native fact of <paramref name="operation" />.</summary>
    public Result Suspend(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef? operationResult,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset suspendedAt, string reason, Uuid episodeId,
        long expectedRevision)
    {
        var accepted = CheckOperation(operation, intent, actorMemberId, actorDisplay);
        if (!accepted.IsSuccess)
            return accepted;
        var prepared = PrepareSuspend(episodeId, expectedRevision, reason);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new MemberSuspended(_tenantId, Id, _userId, actorMemberId, actorDisplay.Trim(), suspendedAt,
            reason.Trim(), operation, intent, null, operationResult));
        return Result.Success;
    }

    /// <summary>
    ///     Checks a reinstatement against hydrated state without raising anything.
    ///     <paramref name="suspensionPending" /> is true when the guard admitted a suspension of this episode that is
    ///     not applied yet, which the reinstatement then waits behind.
    /// </summary>
    public Result PrepareReinstate(Uuid episodeId, long expectedRevision, bool suspensionPending = false)
    {
        EnsureUserIdentity();
        var current = CheckEpisode(episodeId, expectedRevision);
        if (!current.IsSuccess)
            return current;
        return _isSuspended || suspensionPending
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The member is not suspended."));
    }

    /// <summary>Accepts the reinstatement as the native fact of <paramref name="operation" />.</summary>
    public Result Reinstate(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef? operationResult,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset reinstatedAt, Uuid episodeId, long expectedRevision)
    {
        var accepted = CheckOperation(operation, intent, actorMemberId, actorDisplay);
        if (!accepted.IsSuccess)
            return accepted;
        var prepared = PrepareReinstate(episodeId, expectedRevision);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new MemberReinstated(_tenantId, Id, _userId, _affiliation!, actorMemberId, actorDisplay.Trim(),
            reinstatedAt, operation, intent, null, operationResult));
        return Result.Success;
    }

    /// <summary>
    ///     Checks a deprovisioning against hydrated state without raising anything.
    ///     <paramref name="deprovisionPending" /> is true when the guard already admitted a deprovisioning of this
    ///     episode that is not applied yet.
    /// </summary>
    public Result PrepareDeprovision(Uuid episodeId, long expectedRevision, string reason, bool deprovisionPending = false)
    {
        EnsureUserIdentity();
        var current = CheckEpisode(episodeId, expectedRevision);
        if (!current.IsSuccess)
            return current;
        if (_membershipEpisodeId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Membership retirement requires its registered episode."));
        if (deprovisionPending)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The member is already deprovisioned."));
        return string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000
            ? Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Enter a deprovision reason of at most 1000 characters."))
            : Result.Success;
    }

    /// <summary>Accepts the deprovisioning as the native fact of <paramref name="operation" />.</summary>
    public Result Deprovision(OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef? operationResult, Uuid actorMemberId, string actorDisplay, DateTimeOffset deprovisionedAt,
        string reason, Uuid episodeId, long expectedRevision)
    {
        var accepted = CheckOperation(operation, intent, actorMemberId, actorDisplay);
        if (!accepted.IsSuccess)
            return accepted;
        if (deprovisionedAt == default)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A deprovision timestamp is required."));
        var prepared = PrepareDeprovision(episodeId, expectedRevision, reason);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new MemberDeprovisioned(_tenantId, Id, _userId, actorMemberId, actorDisplay.Trim(), deprovisionedAt,
            reason.Trim(), _membershipEpisodeId, operation, intent, null, operationResult));
        return Result.Success;
    }

    /// <summary>The provenance of an operation on this owner; the owner is its own first, affected and revision scope.</summary>
    public OperationProvenance Provenance(Uuid logicalId, Uuid rootOperationId, Uuid? causationEventId,
        string commandKind, ActorReference actor, string episode, ulong? originalExpectedRevision,
        string authoredIntentSha256, DateTimeOffset acceptedAt) =>
        new(logicalId, rootOperationId, causationEventId, commandKind, actor, Stream, episode,
            originalExpectedRevision, authoredIntentSha256, acceptedAt, Stream, Stream);

    /// <summary>Logical revision of this member: the number of facts accepted on the owner so far.</summary>
    public ulong MemberRevision => Version;

    /// <summary>
    ///     Opens a member stream by member identity to accept a process receiver's logical operation. The user is
    ///     learned from the member's own history.
    /// </summary>
    public static Member ForOperation(Uuid tenantId, Uuid memberId, Uuid selectedCommandId) =>
        new(tenantId, memberId, Uuid.Empty, Uuid.Empty, selectedCommandId);

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

    Result CheckEpisode(Uuid episodeId, long expectedRevision)
    {
        if (!_isRegistered)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant member was not found."));
        if (episodeId != Uuid.Empty && episodeId != _membershipEpisodeId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The member is on a different membership episode."));
        return expectedRevision >= 0 && (ulong)expectedRevision != Version
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict, "The member changed since it was read."))
            : Result.Success;
    }

    Result CheckOperation(OperationProvenance operation, ImmutableContentRef intent, Uuid actorMemberId,
        string actorDisplay)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A member lifecycle change requires its own logical operation."));
        return actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(actorDisplay)
            ? Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A member identity is required for the lifecycle history."))
            : Result.Success;
    }

    public Result RequestPersonLink(OperationProvenance operation, ImmutableContentRef firstResponse,
        Uuid personId, long expectedGeneration)
    {
        var selected = PersonLinkOperation(operation, firstResponse);
        if (selected is { } accepted)
            return accepted;
        if (!_isRegistered || _affiliation == "firm_staff")
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The workforce member was not found."));
        if (personId == Uuid.Empty || expectedGeneration < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A person and nonnegative link generation are required."));
        if (_linkedPersonId is not null || _pendingPersonId is not null || expectedGeneration != _personLinkGeneration)
        {
            RaiseEvent(new MemberPersonLinkRefused(_tenantId, Id, _userId, personId, operation.LogicalId,
                expectedGeneration, _personLinkGeneration, "The member is already linked, has a pending admission, or its generation changed.",
                operation, firstResponse));
            return Result.Success;
        }
        RaiseEvent(new MemberPersonLinkAdmitted(_tenantId, Id, _userId, personId, operation.LogicalId,
            _personLinkGeneration + 1, operation, firstResponse));
        return Result.Success;
    }

    public Result ConfirmPersonLink(OperationProvenance operation, ImmutableContentRef firstResponse,
        VerifiedSourceProof<PersonLinkedToMember> source) => ConcludePersonLink(operation, firstResponse,
            source, source.Facts.Header, source.Facts.Link, source.Facts.Operation, "confirm");

    public Result CancelPersonLinkAdmission(OperationProvenance operation, ImmutableContentRef firstResponse,
        VerifiedSourceProof<PersonMemberLinkRefused> source) => ConcludePersonLink(operation, firstResponse,
            source, source.Facts.Header, source.Facts.Link, source.Facts.Operation, "cancel");

    public Result ReleasePersonLink(OperationProvenance operation, ImmutableContentRef firstResponse,
        VerifiedSourceProof<PersonUnlinkedFromMember> source) => ConcludePersonLink(operation, firstResponse,
            source, source.Facts.Header, source.Facts.Link, source.Facts.Operation, "release");

    Result ConcludePersonLink<T>(OperationProvenance operation, ImmutableContentRef firstResponse,
        VerifiedSourceProof<T> source, PersonHeader person, PersonMemberLink link, OperationProvenance sourceOperation,
        string conclusion) where T : DomainEvent
    {
        var selected = PersonLinkOperation(operation, firstResponse);
        if (selected is { } accepted)
            return accepted;
        if (source.EventId == Uuid.Empty || source.EventId != operation.CausationEventId ||
            source.Source != new EventStreamAddress(_tenantId.ToString(), "people", person.PersonId.ToString()) ||
            person.TenantId != _tenantId || link.UserId != _userId || link.MemberId != Id ||
            link.Generation != _personLinkGeneration || _personLinkAdmission is not { } admission ||
            link.LinkId != admission.LinkId || link.AdmissionEventId != admission.Metadata.EventId ||
            person.PersonId != admission.PersonId || sourceOperation.RootOperationId != link.LinkId ||
            (conclusion == "confirm" || conclusion == "cancel" ? _pendingPersonId : _pendingPersonId ?? _linkedPersonId) != person.PersonId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The person conclusion must match the exact current member admission."));
        var reference = new NativeFactReference(source.Source, source.EventId, source.OwnerRevision);
        switch (conclusion)
        {
            case "confirm":
                RaiseEvent(new MemberPersonLinkConfirmed(_tenantId, Id, _userId, person.PersonId, link, reference,
                    operation, firstResponse));
                break;
            case "cancel":
                RaiseEvent(new MemberPersonLinkAdmissionCancelled(_tenantId, Id, _userId, person.PersonId, link,
                    _personLinkGeneration + 1, reference, operation, firstResponse));
                break;
            default:
                RaiseEvent(new MemberPersonLinkReleased(_tenantId, Id, _userId, person.PersonId, link,
                    _personLinkGeneration + 1, reference, operation, firstResponse));
                break;
        }
        return Result.Success;
    }

    Result? PersonLinkOperation(OperationProvenance operation, ImmutableContentRef firstResponse)
    {
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream || firstResponse is null)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A member link requires its own identified operation."));
        if (Acceptance.SelectedLogicalId != operation.LogicalId)
            return null;
        return Acceptance.Decide(AcceptedOperationBinding.Of(operation)) switch
        {
            NativeAcceptanceDecision.Replayed => Result.Success,
            NativeAcceptanceDecision.Conflict => Result.Failure(new RequestError(RequestErrorKind.Conflict, "The member link operation has different intent.")),
            _ => null,
        };
    }

    void RequirePersonConclusion(Uuid tenant, Uuid member, Uuid user, Uuid person, PersonMemberLink link)
    {
        RequireMemberIdentity(tenant, member, user);
        if (_personLinkAdmission is not { } admission || person != admission.PersonId || link.UserId != user ||
            link.MemberId != member || link.LinkId != admission.LinkId || link.Generation != _personLinkGeneration ||
            link.AdmissionEventId != admission.Metadata.EventId)
            throw new MemberHistoryIntegrityException("The person conclusion differs from the member's exact admitted binding.");
    }

    void RequireOwnOperation(OperationProvenance operation)
    {
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            throw new MemberHistoryIntegrityException("A member lifecycle fact must accept its own logical operation on this owner.");
    }

    void RequireMemberIdentity(Uuid tenantId, Uuid memberId, Uuid userId)
    {
        if (tenantId != _tenantId || memberId != Id || userId == Uuid.Empty || userId != _userId)
            throw new MemberHistoryIntegrityException("The member lifecycle fact must retain the exact tenant and user identity.");
    }

    void EnsureUserIdentity()
    {
        if (_userId == Uuid.Empty)
            throw new InvalidOperationException(
                "A member opened for verification cannot record membership changes.");
    }
}
