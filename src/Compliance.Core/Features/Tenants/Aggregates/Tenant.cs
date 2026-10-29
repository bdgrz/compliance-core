using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
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
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
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
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.Tenants.Aggregates;

public sealed class Tenant : Aggregate
{
    bool _registered;
    string? _slug;
    TenantSlugState _slugState;
    bool _suspended;
    bool _offboarding;
    bool _requiresInvitation;
    bool _requiresActivation;
    bool _creatorIsAdministrator;
    bool _activated;
    bool _slugEverConfirmed;
    string? _firstAdministratorEmail;
    Uuid _ownerUserId;
    Uuid _operatorUserId;
    string? _pendingSlug;
    ActorReference? _pendingSlugChangeRequester;
    DateTimeOffset? _pendingSlugChangeRequestedAt;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The slug outcome answers a tenant that moved on; the receiver is a terminal no-op.</summary>
    public static RequestError StaleSlugOutcome { get; } = new(RequestErrorKind.Conflict,
        "The tenant is not waiting for that slug outcome.", false);

    /// <summary>The requested slug is already the tenant's current slug; nothing changes.</summary>
    public static RequestError SlugUnchanged { get; } = new(RequestErrorKind.Conflict,
        "The tenant already uses that slug.", false);

    /// <summary>Logical revision of the tenant: the number of facts accepted on the owner so far.</summary>
    public ulong Revision => Version;

    public Tenant(Uuid id, Uuid selectedCommandId = default)
        : base(id, new EventStreamAddress("bdgrz", "tenants", id.ToString()))
    {
        Acceptance = new NativeAcceptance(selectedCommandId);
        On<TenantRegistered>(Apply);
        On<TenantSlugConfirmed>(Apply);
        On<TenantSlugRejected>(Apply);
        On<TenantSlugSurrenderRequested>(Apply);
        On<TenantSlugSurrenderConfirmed>(Apply);
        On<TenantSlugSurrenderFailed>(Apply);
        On<TenantSuspended>(_ => _suspended = true);
        On<TenantSuspendedV2>(ev =>
        {
            RequireLifecycle(ev, nameof(SuspendTenant), ev.TenantId, ev.OperatorUserId, ev.Reason, ev.OccurredAt,
                ev.OperationResult);
            _suspended = true;
        });
        On<TenantReactivated>(_ => _suspended = false);
        On<TenantReactivatedV2>(ev =>
        {
            RequireLifecycle(ev, nameof(ReactivateTenant), ev.TenantId, ev.OperatorUserId, ev.Reason, ev.OccurredAt,
                ev.OperationResult);
            _suspended = false;
        });
        On<TenantOffboardingStarted>(_ => _offboarding = true);
        On<TenantOffboardingStartedV2>(ev =>
        {
            RequireLifecycle(ev, nameof(OffboardTenant), ev.TenantId, ev.OperatorUserId, ev.Reason, ev.OccurredAt,
                ev.OperationResult);
            _offboarding = true;
        });
        On<TenantActivated>(_ => _activated = true);
        On<TenantActivatedV2>(Apply);
        On<TenantSlugChangeRequested>(ev =>
        {
            Accept(ev, ev.CommandId, ev.Operation);
            _pendingSlug = ev.NewSlug;
            _pendingSlugChangeRequester = ev.RequestedBy;
            _pendingSlugChangeRequestedAt = ev.RequestedAt;
        });
        On<TenantSlugChanged>(ev =>
        {
            Accept(ev, ev.CommandId, ev.Operation);
            _slug = ev.NewSlug;
            _pendingSlug = null;
            _pendingSlugChangeRequester = null;
            _pendingSlugChangeRequestedAt = null;
        });
        On<TenantSlugChangeRejected>(ev =>
        {
            Accept(ev, ev.CommandId, ev.Operation);
            _pendingSlug = null;
            _pendingSlugChangeRequester = null;
            _pendingSlugChangeRequestedAt = null;
        });
    }

    public bool IsActive => _slugState == TenantSlugState.Confirmed && !_suspended && !_offboarding &&
                            (!_requiresActivation || _activated);
    public bool IsRegistered => _slug is not null && _slugState != TenantSlugState.Rejected;
    public bool IsRegistrationRejected => _slugState == TenantSlugState.Rejected;

    /// <summary>True once a confirmed tenant needs no further activation.</summary>
    public bool HasCompletedActivation => _slugState == TenantSlugState.Confirmed &&
                                          (!_requiresActivation || _activated);
    public string? CurrentSlug => _slug;
    public Uuid OwnerUserId => _ownerUserId;
    public bool NeedsCreatorActivation => _creatorIsAdministrator && _requiresActivation && !_activated;
    public Uuid OperatorUserId => _operatorUserId;
    public bool IsSuspended => _suspended;
    public bool IsOffboarding => _offboarding;

    /// <summary>
    ///     True once the tenant has served as an active workspace: its slug was confirmed and any required
    ///     activation completed. Later slug transitions, suspension or offboarding do not undo it.
    /// </summary>
    bool HasEnteredService => _slugEverConfirmed && (!_requiresActivation || _activated);

    public bool WasRegisteredByCreator(Uuid userId) => _creatorIsAdministrator && _ownerUserId == userId;

    /// <summary>Initial provisioning outcome; completion grants no current workspace access.</summary>
    public TenantProvisioningStatusView GetProvisioningStatus() => IsRegistrationRejected
        ? new(Id, "rejected", "slug_unavailable")
        : new(Id, _activated || (_slugEverConfirmed && !_requiresActivation) ? "completed" : "provisioning", null);

    public Result<TenantRegistration> Register(Uuid ownerUserId, string name, string slug,
        string? legalName = null, string? firstAdministratorEmail = null,
        bool creatorIsAdministrator = false)
    {
        if (ownerUserId == Uuid.Empty)
            return Failure<TenantRegistration>(RequestErrorKind.Validation, "A tenant requires an owner.");
        if (string.IsNullOrWhiteSpace(name))
            return Failure<TenantRegistration>(RequestErrorKind.Validation, "A tenant requires a name.");
        if (!TenantSlugs.TryNormalize(slug, out var normalizedSlug, out var slugReason))
            return Failure<TenantRegistration>(RequestErrorKind.Validation, slugReason);
        string? normalizedEmail = null;
        if (firstAdministratorEmail is not null &&
            !EmailAddresses.TryNormalize(firstAdministratorEmail, out normalizedEmail))
            return Failure<TenantRegistration>(RequestErrorKind.Validation, "Enter a valid first administrator email address.");
        if (creatorIsAdministrator && normalizedEmail is not null)
            return Failure<TenantRegistration>(RequestErrorKind.Validation,
                "Creator administration cannot also invite a first administrator.");
        if (_registered)
            return Failure<TenantRegistration>(RequestErrorKind.Conflict, "The tenant is already registered.");

        RaiseEvent(new TenantRegistered(Id, ownerUserId, name.Trim(), normalizedSlug,
            string.IsNullOrWhiteSpace(legalName) ? name.Trim() : legalName.Trim(),
            normalizedEmail, creatorIsAdministrator, ActivationRequired: creatorIsAdministrator));
        return Result<TenantRegistration>.Success(new TenantRegistration(Id, normalizedSlug));
    }

    /// <summary>A bootstrap reaction for an already completed/rejected registration needs no state transition.</summary>
    public static RequestError ActivationNotNeeded { get; } = new(RequestErrorKind.Conflict,
        "The tenant registration no longer needs an activation decision.", false);

    public Result PrepareActivation(Uuid userId, string? email)
    {
        if (IsRegistrationRejected || HasCompletedActivation)
            return Result.Failure(ActivationNotNeeded);
        return ValidateActivation(userId, email);
    }

    public Result Activate(OperationProvenance operation, ImmutableContentRef response, Uuid userId, string? email)
    {
        var prepared = PrepareActivation(userId, email);
        if (!prepared.IsSuccess)
            return prepared;
        if (!ValidActivationOperation(operation) || response is null)
            return Failure(RequestErrorKind.Validation, "Activation requires its exact native bootstrap operation.");
        RaiseEvent(new TenantActivatedV2(Id, userId, email, operation, response));
        return Result.Success;
    }

    bool ValidActivationOperation(OperationProvenance operation) => operation.Owner == Stream &&
        operation.AffectedOwner == Stream && operation.ExpectedRevisionOwner == Stream &&
        operation.LogicalId != Uuid.Empty && operation.RootOperationId == operation.LogicalId &&
        operation.CommandKind == "ActivateTenant" && operation.Episode == "activation" &&
        operation.OriginalExpectedRevision is null && operation.Actor is { Kind: "system_process", Id: "system" };

    void Apply(TenantActivatedV2 ev)
    {
        if (ev.TenantId != Id || ev.FirstResponse is null || !ValidActivationOperation(ev.Operation) ||
            !PrepareActivation(ev.FirstAdministratorUserId, ev.FirstAdministratorEmail).IsSuccess)
            throw new InvalidOperationException("A native tenant activation must follow its exact eligible bootstrap.");
        Accept(ev, ev.Operation.LogicalId, ev.Operation);
        _activated = true;
    }

    public Result Activate(Uuid firstAdministratorUserId, string? firstAdministratorEmail)
    {
        var valid = ValidateActivation(firstAdministratorUserId, firstAdministratorEmail);
        if (!valid.IsSuccess)
            return valid;
        if (_activated)
            return Result.Success;
        RaiseEvent(new TenantActivated(Id, firstAdministratorUserId));
        return Result.Success;
    }

    Result ValidateActivation(Uuid firstAdministratorUserId, string? firstAdministratorEmail)
    {
        if (_offboarding)
            return Failure(RequestErrorKind.Conflict, "The tenant is being offboarded.");
        if (!_requiresActivation)
            return Failure(RequestErrorKind.Conflict, "The tenant is not ready to activate.");
        if (_slugState != TenantSlugState.Confirmed)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The tenant is not ready to activate.",
                isTransient: _slugState == TenantSlugState.Pending));
        if (firstAdministratorUserId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "A first administrator is required.");
        if (_creatorIsAdministrator &&
            (firstAdministratorUserId != _ownerUserId || firstAdministratorEmail is not null))
            return Failure(RequestErrorKind.Forbidden, "Only the verified creator may activate.");
        if (_requiresInvitation &&
            (!EmailAddresses.TryNormalize(firstAdministratorEmail, out var normalized) ||
             normalized != _firstAdministratorEmail))
            return Failure(RequestErrorKind.Forbidden, "Only the invited first administrator may activate.");
        return Result.Success;
    }

    /// <summary>
    ///     Checks that the tenant waits for the confirmed slug. A receiver that arrives after the tenant moved on
    ///     reports <see cref="StaleSlugOutcome" />, which dispatchers treat as a terminal no-op.
    /// </summary>
    public Result PrepareConfirmSlug(string slug) =>
        MatchesPendingChange(slug) || MatchesPending(slug) ? Result.Success : Result.Failure(StaleSlugOutcome);

    /// <summary>Accepts the confirmation as the native fact of <paramref name="operation" />.</summary>
    public Result ConfirmSlug(OperationProvenance operation, ImmutableContentRef firstResponse, string slug)
    {
        var prepared = PrepareReceiver(operation, firstResponse, PrepareConfirmSlug(slug));
        if (!prepared.IsSuccess)
            return prepared;
        if (_pendingSlug is not null && MatchesPendingChange(slug))
            RaiseEvent(new TenantSlugChanged(Id, _slug!, _pendingSlug, operation.LogicalId, operation, firstResponse)
            {
                RequestedBy = _pendingSlugChangeRequester,
                RequestedAt = _pendingSlugChangeRequestedAt,
            });
        else
            RaiseEvent(new TenantSlugConfirmed(Id, _slug!, operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    /// <summary>Checks that the tenant waits for the refused slug; see <see cref="PrepareConfirmSlug" />.</summary>
    public Result PrepareRejectSlug(string slug) =>
        MatchesPendingChange(slug) || MatchesPending(slug) ? Result.Success : Result.Failure(StaleSlugOutcome);

    /// <summary>Accepts the refusal as the native fact of <paramref name="operation" />.</summary>
    public Result RejectSlug(OperationProvenance operation, ImmutableContentRef firstResponse, string slug)
    {
        var prepared = PrepareReceiver(operation, firstResponse, PrepareRejectSlug(slug));
        if (!prepared.IsSuccess)
            return prepared;
        if (_pendingSlug is not null && MatchesPendingChange(slug))
            RaiseEvent(new TenantSlugChangeRejected(Id, _slug!, _pendingSlug, operation.LogicalId, operation,
                firstResponse));
        else
            RaiseEvent(new TenantSlugRejected(Id, _slug!, operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    /// <summary>
    ///     Checks a slug change against hydrated state without raising anything. A negative expected revision is
    ///     unconstrained and reserved for system callers.
    /// </summary>
    public Result PrepareRequestSlugChange(string slug, long expectedRevision)
    {
        if (!TenantSlugs.TryNormalize(slug, out var normalized, out var reason))
            return Failure(RequestErrorKind.Validation, reason);
        if (expectedRevision >= 0 && (ulong)expectedRevision != Revision)
            return Failure(RequestErrorKind.Conflict, "The tenant changed since it was read.");
        if (!IsActive)
            return Failure(RequestErrorKind.Conflict, "The tenant is not active.");
        if (normalized == _slug)
            return Result.Failure(SlugUnchanged);
        if (_pendingSlug is not null)
            return Failure(RequestErrorKind.Conflict, "A slug change is already pending.");
        return Result.Success;
    }

    /// <summary>Accepts the slug change request as the native fact of <paramref name="operation" />.</summary>
    public Result RequestSlugChange(OperationProvenance operation, ImmutableContentRef firstResponse, string slug,
        long expectedRevision, ActorReference? requestedBy = null, DateTimeOffset? requestedAt = null)
    {
        var prepared = PrepareReceiver(operation, firstResponse, PrepareRequestSlugChange(slug, expectedRevision));
        if (!prepared.IsSuccess)
            return prepared;
        TenantSlugs.TryNormalize(slug, out var normalized);
        RaiseEvent(new TenantSlugChangeRequested(Id, _slug!, normalized, operation.LogicalId, operation,
            firstResponse)
        {
            RequestedBy = requestedBy,
            RequestedAt = requestedAt,
        });
        return Result.Success;
    }

    /// <summary>Checks a slug surrender against hydrated state without raising anything.</summary>
    public Result PrepareRequestSlugSurrender(string slug)
    {
        if (_offboarding)
            return Failure(RequestErrorKind.Conflict, "The tenant is being offboarded.");
        if (_slugState != TenantSlugState.Confirmed || !Matches(slug))
            return Failure(RequestErrorKind.Conflict, "The tenant does not own that slug.");
        return Result.Success;
    }

    /// <summary>Accepts the surrender request as the native fact of <paramref name="operation" />.</summary>
    public Result RequestSlugSurrender(OperationProvenance operation, ImmutableContentRef firstResponse, string slug)
    {
        var prepared = PrepareReceiver(operation, firstResponse, PrepareRequestSlugSurrender(slug));
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new TenantSlugSurrenderRequested(Id, _slug!, operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    /// <summary>Checks a fresh native lifecycle decision on the exact current owner.</summary>
    public Result PrepareLifecycle(string kind, string reason, long? expectedRevision)
    {
        var valid = CheckLifecycle(kind, reason, expectedRevision);
        if (!valid.IsSuccess)
            return valid;
        if (kind == nameof(OffboardTenant) && !_offboarding && !_suspended && !HasEnteredService)
            return Failure(RequestErrorKind.Conflict, "Only an active or suspended tenant can start offboarding.");
        return Result.Success;
    }

    /// <summary>
    ///     The checks a committed lifecycle fact must still satisfy on replay. The offboarding source-state rule is
    ///     applied to fresh decisions only, so history committed before it stays hydratable.
    /// </summary>
    Result CheckLifecycle(string kind, string reason, long? expectedRevision)
    {
        if (!TryNormalizeReason(reason, out _))
            return Failure(RequestErrorKind.Validation, "A reason of at most 500 characters is required.");
        if (kind is not (nameof(SuspendTenant) or nameof(ReactivateTenant) or nameof(OffboardTenant)))
            return Failure(RequestErrorKind.Validation, "The tenant lifecycle command is unsupported.");
        if (kind == nameof(ReactivateTenant) ? expectedRevision is not null : expectedRevision is null or < 0)
            return Failure(RequestErrorKind.Validation, "The lifecycle command needs its declared owner revision scope.");
        if (expectedRevision is { } expected && (ulong)expected != Revision)
            return Failure(RequestErrorKind.Conflict, "The tenant changed since it was read.");
        if (_slug is null || _slugState == TenantSlugState.Rejected)
            return Failure(RequestErrorKind.NotFound, "The tenant does not exist.");
        if (kind != nameof(OffboardTenant) && _offboarding)
            return Failure(RequestErrorKind.Conflict, "The tenant is being offboarded.");
        if (kind == nameof(SuspendTenant) && !_suspended && !IsActive)
            return Failure(RequestErrorKind.Conflict, "The tenant is not active.");
        if (kind == nameof(ReactivateTenant) && (_slugState != TenantSlugState.Confirmed ||
            _requiresActivation && !_activated))
            return Failure(RequestErrorKind.Conflict, "The tenant is not ready to reactivate.");
        return Result.Success;
    }

    /// <summary>Records even an unchanged fresh decision, so its original outcome remains durable after later changes.</summary>
    public Result ApplyLifecycle(OperationProvenance operation, ImmutableContentRef response,
        ImmutableContentRef result, string reason)
    {
        var expected = operation.OriginalExpectedRevision is { } revision && revision <= long.MaxValue
            ? (long?)revision : null;
        var valid = PrepareLifecycle(operation.CommandKind, reason, expected);
        if (!valid.IsSuccess)
            return valid;
        if (!Uuid.TryParse(operation.Actor.Id, out var userId) || userId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "A lifecycle decision requires its platform operator.");
        TryNormalizeReason(reason, out var normalized);
        DomainEvent fact = operation.CommandKind switch
        {
            nameof(SuspendTenant) => new TenantSuspendedV2(Id, userId, normalized, operation.AcceptedAt,
                operation, response, result),
            nameof(ReactivateTenant) => new TenantReactivatedV2(Id, userId, normalized, operation.AcceptedAt,
                operation, response, result),
            _ => new TenantOffboardingStartedV2(Id, userId, normalized, operation.AcceptedAt,
                operation, response, result),
        };
        RaiseEvent(fact);
        return Result.Success;
    }

    void RequireLifecycle<T>(T fact, string kind, Uuid tenantId, Uuid operatorId, string reason,
        DateTimeOffset at, ImmutableContentRef result) where T : DomainEvent, INativeOperationFact
    {
        var operation = fact.Operation;
        OwnerProvenance.RequireOwn(this, operation.LogicalId, operation);
        var expected = operation.OriginalExpectedRevision is { } revision && revision <= long.MaxValue
            ? (long?)revision : null;
        if (tenantId != Id || operatorId == Uuid.Empty || operation.Actor.Kind != "platform_operator" ||
            operation.Actor.Id != operatorId.ToString() || operation.RootOperationId != operation.LogicalId ||
            operation.CausationEventId is not null || operation.CommandKind != kind ||
            operation.Episode != "lifecycle" || operation.AffectedOwner != Stream ||
            operation.ExpectedRevisionOwner != Stream || fact.FirstResponse is null || result is null ||
            result.Schema != "mutation-result-content.v1" || at == default || at != operation.AcceptedAt ||
            !TryNormalizeReason(reason, out var normalized) || normalized != reason ||
            !CheckLifecycle(kind, reason, expected).IsSuccess)
            throw new InvalidOperationException("A lifecycle fact must retain its exact current owner and platform decision.");
        Acceptance.Observe(fact);
    }

    public Result Suspend(Uuid operatorUserId, string reason, DateTimeOffset occurredAt)
    {
        if (!TryNormalizeReason(reason, out var normalizedReason))
            return Failure(RequestErrorKind.Validation, "A reason of at most 500 characters is required.");
        if (_slug is null || _slugState == TenantSlugState.Rejected)
            return Failure(RequestErrorKind.NotFound, "The tenant does not exist.");
        if (_offboarding)
            return Failure(RequestErrorKind.Conflict, "The tenant is being offboarded.");
        if (_suspended)
            return Result.Success;
        if (!IsActive)
            return Failure(RequestErrorKind.Conflict, "The tenant is not active.");
        RaiseEvent(new TenantSuspended(Id, operatorUserId)
        {
            Reason = normalizedReason,
            OccurredAt = occurredAt,
        });
        return Result.Success;
    }

    public Result Reactivate(Uuid operatorUserId, string reason, DateTimeOffset occurredAt)
    {
        if (!TryNormalizeReason(reason, out var normalizedReason))
            return Failure(RequestErrorKind.Validation, "A reason of at most 500 characters is required.");
        if (_slug is null || _slugState == TenantSlugState.Rejected)
            return Failure(RequestErrorKind.NotFound, "The tenant does not exist.");
        if (_offboarding)
            return Failure(RequestErrorKind.Conflict, "The tenant is being offboarded.");
        if (_slugState != TenantSlugState.Confirmed ||
            _requiresActivation && !_activated)
            return Failure(RequestErrorKind.Conflict, "The tenant is not ready to reactivate.");
        if (_suspended)
            RaiseEvent(new TenantReactivated(Id, operatorUserId)
            {
                Reason = normalizedReason,
                OccurredAt = occurredAt,
            });
        return Result.Success;
    }

    public Result StartOffboarding(Uuid operatorUserId, string reason, DateTimeOffset startedAt)
    {
        if (!TryNormalizeReason(reason, out var normalizedReason))
            return Failure(RequestErrorKind.Validation, "A reason of at most 500 characters is required.");
        if (_slug is null || _slugState == TenantSlugState.Rejected)
            return Failure(RequestErrorKind.NotFound, "The tenant does not exist.");
        if (_offboarding)
            return Result.Success;
        if (!_suspended && !HasEnteredService)
            return Failure(RequestErrorKind.Conflict, "Only an active or suspended tenant can start offboarding.");

        RaiseEvent(new TenantOffboardingStarted(Id, operatorUserId, normalizedReason, startedAt));
        return Result.Success;
    }

    /// <summary>Checks that the tenant is surrendering the released slug; see <see cref="PrepareConfirmSlug" />.</summary>
    public Result PrepareConfirmSlugSurrender(string slug) =>
        _slugState == TenantSlugState.Surrendering && Matches(slug) ? Result.Success : Result.Failure(StaleSlugOutcome);

    /// <summary>Accepts the release as the native fact of <paramref name="operation" />.</summary>
    public Result ConfirmSlugSurrender(OperationProvenance operation, ImmutableContentRef firstResponse, string slug)
    {
        var prepared = PrepareReceiver(operation, firstResponse, PrepareConfirmSlugSurrender(slug));
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new TenantSlugSurrenderConfirmed(Id, _slug!, operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    /// <summary>Checks that the tenant is surrendering the refused slug; see <see cref="PrepareConfirmSlug" />.</summary>
    public Result PrepareRejectSlugSurrender(string slug) =>
        _slugState == TenantSlugState.Surrendering && Matches(slug) ? Result.Success : Result.Failure(StaleSlugOutcome);

    /// <summary>Accepts the refusal as the native fact of <paramref name="operation" />.</summary>
    public Result RejectSlugSurrender(OperationProvenance operation, ImmutableContentRef firstResponse, string slug)
    {
        var prepared = PrepareReceiver(operation, firstResponse, PrepareRejectSlugSurrender(slug));
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new TenantSlugSurrenderFailed(Id, _slug!, operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    /// <summary>The provenance of a root operation on this owner.</summary>
    public OperationProvenance Provenance(Uuid commandId, string commandKind, ActorReference actor, string episode,
        ulong? originalExpectedRevision, string authoredIntentSha256, DateTimeOffset acceptedAt) =>
        OwnerProvenance.For(this, commandId, commandKind, actor, episode, originalExpectedRevision,
            authoredIntentSha256, acceptedAt);

    Result PrepareReceiver(OperationProvenance operation, ImmutableContentRef firstResponse, Result prepared)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Failure(RequestErrorKind.Validation, "A tenant slug operation requires its own logical operation.");
        return prepared;
    }

    void Accept(DomainEvent fact, Uuid commandId, OperationProvenance operation)
    {
        OwnerProvenance.RequireOwn(this, commandId, operation);
        Acceptance.Observe(fact);
    }

    bool Matches(string slug) => TenantSlugs.TryNormalize(slug, out var normalized) && normalized == _slug;
    bool MatchesPending(string slug) => _slugState == TenantSlugState.Pending && Matches(slug);
    bool MatchesPendingChange(string slug) =>
        TenantSlugs.TryNormalize(slug, out var normalized) && normalized == _pendingSlug;

    void Apply(TenantRegistered registered)
    {
        _registered = true;
        _slug = registered.Slug;
        _slugState = TenantSlugState.Pending;
        _requiresInvitation = registered.FirstAdministratorEmail is not null &&
                              !registered.CreatorIsAdministrator;
        _requiresActivation = _requiresInvitation || registered.ActivationRequired;
        _creatorIsAdministrator = registered.CreatorIsAdministrator;
        _firstAdministratorEmail = registered.FirstAdministratorEmail;
        _ownerUserId = registered.OwnerUserId;
        _operatorUserId = registered.CreatorIsAdministrator ? Uuid.Empty : registered.OwnerUserId;
    }
    void Apply(TenantSlugConfirmed confirmed)
    {
        Accept(confirmed, confirmed.CommandId, confirmed.Operation);
        _slugState = TenantSlugState.Confirmed;
        _slugEverConfirmed = true;
    }
    void Apply(TenantSlugRejected rejected)
    {
        Accept(rejected, rejected.CommandId, rejected.Operation);
        _slugState = TenantSlugState.Rejected;
    }
    void Apply(TenantSlugSurrenderRequested requested)
    {
        Accept(requested, requested.CommandId, requested.Operation);
        _slugState = TenantSlugState.Surrendering;
    }
    void Apply(TenantSlugSurrenderConfirmed confirmed)
    {
        Accept(confirmed, confirmed.CommandId, confirmed.Operation);
        _slug = null;
        _slugState = TenantSlugState.None;
    }
    void Apply(TenantSlugSurrenderFailed failed)
    {
        Accept(failed, failed.CommandId, failed.Operation);
        _slugState = TenantSlugState.Confirmed;
    }

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
    static Result<T> Failure<T>(RequestErrorKind kind, string message) => Result<T>.Failure(new RequestError(kind, message));

    static bool TryNormalizeReason(string? reason, out string normalized)
    {
        normalized = reason?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= 500;
    }

    enum TenantSlugState { None, Pending, Confirmed, Rejected, Surrendering }
}
