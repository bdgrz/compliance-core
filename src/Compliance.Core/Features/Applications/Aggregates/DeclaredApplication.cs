using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.Applications.Aggregates;

/// <summary>Owns one tenant-declared application and its metadata history.</summary>
public sealed partial class DeclaredApplication : Aggregate
{
    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    string? _initialName;
    string? _initialPurpose;
    string? _initialOwnerReference;
    string? _initialClassification;
    string? _initialApplicationKind;
    string? _initialCriticality;
    Uuid? _initialSystemOwner;
    Uuid? _initialAccessOwner;
    bool _initialIsRestricted;

    bool _retired;
    bool _isRestricted;
    string? _classification;
    string? _applicationKind;
    string? _criticality;
    Uuid? _systemOwnerPersonId;

    public bool IsCreated => _created;
    public bool IsRetired => _retired;
    public long Revision => _revision;
    public Uuid? AccessOwnerPersonId { get; private set; }
    public bool IsRestricted => _isRestricted;

    public DeclaredApplication(Uuid tenantId, Uuid applicationId, Uuid selectedLogicalId = default)
        : base(applicationId, new EventStreamAddress(tenantId.ToString(), Area, applicationId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        RegisterImportEffectEvents();
        On<ApplicationDeclared>(ev =>
        {
            Acceptance.Observe(ev);
            RequireOwnOperation(ev.Operation);
            if (_created)
                throw new InvalidOperationException("The application is already declared.");
            _created = true;
            _revision = 1;
            _initialName = ev.Name;
            _initialPurpose = ev.Purpose;
            _initialOwnerReference = ev.OwnerReference;
            _initialClassification = ev.Classification;
            _initialApplicationKind = ev.ApplicationKind;
            _initialCriticality = ev.Criticality;
            _initialSystemOwner = ev.SystemOwnerPersonId;
            _initialAccessOwner = ev.AccessOwnerPersonId;
            _initialIsRestricted = ev.IsRestricted;
            _isRestricted = ev.IsRestricted;
            _classification = ev.Classification;
            _applicationKind = ev.ApplicationKind;
            _criticality = ev.Criticality;
            _systemOwnerPersonId = ev.SystemOwnerPersonId;
            AccessOwnerPersonId = ev.AccessOwnerPersonId;
        });
        On<ApplicationRevised>(ev =>
        {
            Acceptance.Observe(ev);
            RequireOwnOperation(ev.Operation);
            _revision = ev.Revision;
            _classification = ev.Classification;
            _systemOwnerPersonId = ev.SystemOwnerPersonId;
            _applicationKind = ev.ApplicationKind ?? _applicationKind;
            _criticality = ev.Criticality ?? _criticality;
            AccessOwnerPersonId = ev.AccessOwnerPersonId;
            _isRestricted = ev.IsRestricted;
        });
        On<ApplicationRetired>(ev =>
        {
            Acceptance.Observe(ev);
            RequireOwnOperation(ev.Operation);
            if (ev.MergedIntoApplicationRevision is { } targetRevision &&
                (ev.MergedIntoApplicationId is not { } successorId || targetRevision < 1 ||
                 Relationship(ApplicationRelationshipIdentity.Replaces, successorId)?.Approval
                     ?.TargetApplicationRevision != targetRevision))
                throw new InvalidOperationException(
                    "An application retirement does not match its approved successor revision.");
            _revision = ev.Revision;
            _retired = true;
        });
        // Historical application-stream declarations still own their old revision numbers.
        // Their instance state is projected separately and is not retained by this aggregate.
        On<SystemInstanceDeclared>(ev => _revision = ev.ApplicationRevision);
        RegisterApplicationRelationshipEvents();
        RegisterGovernedRelationshipEvents();
    }

    /// <summary>
    ///     Checks the creation guards against hydrated state without raising anything: the identity must be free
    ///     (not created, not reserved by an import plan) and the content valid. An exact retry never reaches this
    ///     check; it resolves from the accepted fact of the same logical id.
    /// </summary>
    public Result PrepareDeclare(string name, string purpose, string? ownerReference, string? classification,
        Uuid? systemOwnerPersonId, Uuid? accessOwnerPersonId, string? applicationKind, string? criticality,
        bool governedDeclarationsEnabled = false)
    {
        var normalizedClassification = NormalizeOptional(classification);
        var normalizedKind = NormalizeOptional(applicationKind);
        var normalizedCriticality = NormalizeOptional(criticality);
        if (!_created && ReservesIdentity)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The application identity is reserved by an import plan."));
        if (_created)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The application already exists with different content."));
        var validation = Validate(name, purpose, ownerReference, classification);
        if (validation is not null)
            return Result.Failure(validation);
        if (applicationKind is { Length: > 200 } || criticality is { Length: > 200 })
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Application kind and criticality cannot exceed 200 characters each."));
        if (governedDeclarationsEnabled && (normalizedClassification is null ||
            systemOwnerPersonId is null || systemOwnerPersonId == Uuid.Empty ||
            accessOwnerPersonId is null || accessOwnerPersonId == Uuid.Empty ||
            normalizedKind is null || normalizedCriticality is null))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An application requires classification, system and access owners, application kind and criticality."));
        return Result.Success;
    }

    /// <summary>Accepts the creation as the native fact of <paramref name="operation" />.</summary>
    public Result Declare(OperationProvenance operation, ImmutableContentRef firstResponse, string name,
        string purpose, string? ownerReference, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt, string? classification = null,
        Uuid? systemOwnerPersonId = null, Uuid? accessOwnerPersonId = null,
        bool isRestricted = false, string? applicationKind = null,
        string? criticality = null, bool governedDeclarationsEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        var owned = CheckOwnOperation(operation);
        if (!owned.IsSuccess)
            return owned;
        var prepared = PrepareDeclare(name, purpose, ownerReference, classification, systemOwnerPersonId,
            accessOwnerPersonId, applicationKind, criticality, governedDeclarationsEnabled);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new ApplicationDeclared(_tenantId, Id, name.Trim(), purpose.Trim(),
            NormalizeOptional(ownerReference), actorMemberId, actorDisplay, changedAt, operation, firstResponse,
            NormalizeOptional(classification), systemOwnerPersonId, accessOwnerPersonId, isRestricted,
            NormalizeOptional(applicationKind), NormalizeOptional(criticality))
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return Result.Success;
    }

    /// <summary>Accepts the revision as the native fact of <paramref name="operation" />.</summary>
    public CommandFailure? Revise(OperationProvenance operation, ImmutableContentRef firstResponse,
        long expectedRevision, string name, string purpose,
        string? ownerReference, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt, string? classification = null,
        Uuid? systemOwnerPersonId = null, Uuid? accessOwnerPersonId = null,
        bool? isRestricted = null, string? applicationKind = null,
        string? criticality = null, bool governedRevisionsEnabled = false,
        ApplicationRevisionField classificationIntent = default,
        ApplicationRevisionField systemOwnerIntent = default,
        ApplicationRevisionField accessOwnerIntent = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        var owned = CheckOwnOperation(operation);
        if (!owned.IsSuccess)
            return CommandFailure.InvalidContent(owned.Error!.Message!);
        var check = ResolveRevisionContent(expectedRevision, name, purpose, ownerReference,
            classification, systemOwnerPersonId, accessOwnerPersonId, isRestricted,
            applicationKind, criticality, governedRevisionsEnabled, classificationIntent,
            systemOwnerIntent, accessOwnerIntent, out var content);
        if (check is not null)
            return check;
        RaiseEvent(new ApplicationRevised(_tenantId, Id, _revision + 1, content.Name,
            content.Purpose, content.OwnerReference, actorMemberId,
            actorDisplay, changedAt, operation, firstResponse, content.Classification, content.SystemOwnerPersonId,
            content.AccessOwnerPersonId, content.IsRestricted, content.ApplicationKind, content.Criticality)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    internal CommandFailure? ResolveRevisionContent(long expectedRevision, string name,
        string purpose, string? ownerReference, string? classification,
        Uuid? systemOwnerPersonId, Uuid? accessOwnerPersonId, bool? isRestricted,
        string? applicationKind, string? criticality, bool governed,
        ApplicationRevisionField classificationIntent, ApplicationRevisionField systemOwnerIntent,
        ApplicationRevisionField accessOwnerIntent,
        out ApplicationRevisionContent content)
    {
        content = new ApplicationRevisionContent(name?.Trim()!, purpose?.Trim()!,
            NormalizeOptional(ownerReference),
            governed ? ApplicationRevisionContent.ResolveField(classification?.Trim(),
                _classification?.Trim(), classificationIntent, governed) : NormalizeOptional(classification),
            ApplicationRevisionContent.ResolveField(systemOwnerPersonId, _systemOwnerPersonId, systemOwnerIntent, governed),
            ApplicationRevisionContent.ResolveField(accessOwnerPersonId, AccessOwnerPersonId, accessOwnerIntent, governed),
            isRestricted ?? _isRestricted, applicationKind?.Trim() ?? _applicationKind?.Trim(),
            criticality?.Trim() ?? _criticality?.Trim());
        var check = CheckChange(expectedRevision);
        if (check is not null)
            return check;
        var validation = Validate(name!, purpose!, ownerReference, classification);
        return validation is not null
            ? CommandFailure.InvalidContent(validation.Message!)
            : content.Validate(governed);
    }

    /// <summary>
    ///     Checks the retirement guards against hydrated state without raising anything: the application exists, is
    ///     current and unheld, the reason is bounded and a successor merge matches its approved relationship.
    /// </summary>
    public CommandFailure? PrepareRetire(long expectedRevision, string reason, Uuid? mergedIntoApplicationId,
        out long? mergedIntoApplicationRevision)
    {
        mergedIntoApplicationRevision = null;
        var check = CheckChange(expectedRevision);
        if (check is not null)
            return check;
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            return CommandFailure.InvalidContent(
                "A retirement requires a reason of at most 2000 characters.");
        if (mergedIntoApplicationId == Id || mergedIntoApplicationId == Uuid.Empty)
            return CommandFailure.InvalidContent(
                "An application cannot be merged into itself or an empty identity.");
        if (mergedIntoApplicationId is { } successorId)
        {
            var relationship = Relationship(ApplicationRelationshipIdentity.Replaces, successorId);
            if (relationship?.Status != "approved" || relationship.Approval is not { } approval ||
                approval.SourceApplicationRevision != expectedRevision ||
                approval.TargetApplicationRevision < 1)
                return CommandFailure.StateConflict(
                    "Merging an application requires an approved successor relationship for the current revision.");
            mergedIntoApplicationRevision = approval.TargetApplicationRevision;
        }
        return null;
    }

    /// <summary>Retires the application from an effective date as the native fact of <paramref name="operation" />.</summary>
    public CommandFailure? Retire(OperationProvenance operation, ImmutableContentRef firstResponse,
        long expectedRevision, DateTimeOffset effectiveAt,
        string reason, Uuid? mergedIntoApplicationId, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        var owned = CheckOwnOperation(operation);
        if (!owned.IsSuccess)
            return CommandFailure.InvalidContent(owned.Error!.Message!);
        var check = PrepareRetire(expectedRevision, reason, mergedIntoApplicationId, out var mergedRevision);
        if (check is not null)
            return check;
        RaiseEvent(new ApplicationRetired(_tenantId, Id, _revision + 1, effectiveAt,
            reason.Trim(), mergedIntoApplicationId, actorMemberId, actorDisplay, changedAt, operation, firstResponse,
            mergedRevision)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    Result CheckOwnOperation(OperationProvenance operation) => operation.LogicalId == Uuid.Empty ||
        operation.Owner != Stream
        ? Result.Failure(new RequestError(RequestErrorKind.Validation,
            "An application operation requires its own logical operation."))
        : Result.Success;

    void RequireOwnOperation(OperationProvenance operation)
    {
        if (!CheckOwnOperation(operation).IsSuccess)
            throw new InvalidOperationException("The application fact does not carry an operation of this owner.");
    }

    CommandFailure? CheckChange(long expectedRevision) => !_created
        ? CommandFailure.MissingRecord("The application was not found.")
        : _retired ? CommandFailure.StateConflict("The application is retired.")
        : CheckPendingImportChanges() is { } importError ? CommandFailure.StateConflict(importError.Message!)
        : expectedRevision == _revision ? null :
            CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("application", _revision));

    static RequestError? Validate(string name, string purpose, string? ownerReference,
        string? classification)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return new RequestError(RequestErrorKind.Validation,
                "An application requires a name of at most 200 characters.");
        if (string.IsNullOrWhiteSpace(purpose) || purpose.Length > 2000)
            return new RequestError(RequestErrorKind.Validation,
                "An application requires a purpose of at most 2000 characters.");
        if (ownerReference is { Length: > 2000 })
            return new RequestError(RequestErrorKind.Validation,
                "An owner reference cannot exceed 2000 characters.");
        if (classification is { Length: > 200 })
            return new RequestError(RequestErrorKind.Validation,
                "A classification cannot exceed 200 characters.");
        return null;
    }

    static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
