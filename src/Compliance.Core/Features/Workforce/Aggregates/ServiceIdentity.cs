using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Aggregates;

/// <summary>
///     Owns one governed non-human identity (M0-D06): exactly one accountable owner, a person or a
///     team, an approved purpose, and a review date at most one year out. Groups and roles are
///     never owners.
/// </summary>
public sealed class ServiceIdentity : Aggregate
{
    static readonly string[] IdentityKinds = ["workload", "service", "automation", "bot", "integration", "shared_account"];
    static readonly string[] LifecycleStatuses = ["active", "disabled", "retired"];
    static readonly string[] OwnerKinds = ["person", "team"];

    readonly Uuid _tenantId;
    bool _created;
    bool _hasRetired;
    long _revision;
    ServiceIdentityTerms? _initialTerms;
    ServiceIdentityTerms? _terms;

    public bool IsCreated => _created;
    public long Revision => _revision;
    public string? OwnerKind => _terms?.OwnerKind;
    public Uuid OwnerId => _terms?.OwnerId ?? Uuid.Empty;

    public ServiceIdentity(Uuid tenantId, Uuid serviceIdentityId)
        : base(serviceIdentityId, new EventStreamAddress(tenantId.ToString(), "service-identities",
            serviceIdentityId.ToString()))
    {
        _tenantId = tenantId;
        On<ServiceIdentityRecorded>(ObserveRecorded);
        On<ServiceIdentityRevised>(ObserveRevised);
        On<ServiceIdentityOwnerChanged>(fact =>
        {
            RequireNamedFact(fact.TenantId, fact.ServiceIdentityId, fact.Revision);
            var current = _terms!;
            if (fact.PreviousOwnerKind != current.OwnerKind || fact.PreviousOwnerId != current.OwnerId ||
                !OwnerKinds.Contains(fact.OwnerKind) || fact.OwnerId == Uuid.Empty ||
                current.IdentityKind == "shared_account" && fact.OwnerKind != "person")
                throw new InvalidOperationException("The named owner change must preserve its exact predecessor and accountable owner.");
            _terms = current with { OwnerKind = fact.OwnerKind, OwnerId = fact.OwnerId };
            _revision = fact.Revision;
        });
        On<ServiceIdentityPurposeChanged>(fact =>
        {
            RequireNamedFact(fact.TenantId, fact.ServiceIdentityId, fact.Revision);
            if (string.IsNullOrWhiteSpace(fact.Purpose) || fact.Purpose.Length > 1000 || fact.Purpose != fact.Purpose.Trim())
                throw new InvalidOperationException("The named purpose must be canonical approved text.");
            _terms = _terms! with { Purpose = fact.Purpose };
            _revision = fact.Revision;
        });
        On<ServiceIdentityReviewed>(fact =>
        {
            RequireNamedFact(fact.TenantId, fact.ServiceIdentityId, fact.Revision);
            var reviewedOn = DateOnly.FromDateTime(fact.ReviewedAt.UtcDateTime);
            if (fact.NextReviewDate <= reviewedOn || fact.NextReviewDate > reviewedOn.AddYears(1) ||
                fact.Notes is { Length: > 1000 })
                throw new InvalidOperationException("The next service identity review must be within one year of this review.");
            _terms = _terms! with { ReviewBy = fact.NextReviewDate };
            _revision = fact.Revision;
        });
        On<ServiceIdentityDisabled>(fact =>
        {
            RequireNamedFact(fact.TenantId, fact.ServiceIdentityId, fact.Revision);
            var current = _terms!;
            if (current.LifecycleStatus != "active" || fact.Reason is { Length: > 1000 })
                throw new InvalidOperationException("A disable fact requires an active identity and bounded reason.");
            _terms = current with { LifecycleStatus = "disabled" };
            _revision = fact.Revision;
        });
        On<ServiceIdentityEnabled>(fact =>
        {
            RequireNamedFact(fact.TenantId, fact.ServiceIdentityId, fact.Revision);
            var current = _terms!;
            if (current.LifecycleStatus != "disabled" || current.ExpiresOn is { } expiry &&
                expiry <= DateOnly.FromDateTime(fact.ChangedAt.UtcDateTime))
                throw new InvalidOperationException("Only a disabled unexpired service identity can be enabled.");
            _terms = current with { LifecycleStatus = "active" };
            _revision = fact.Revision;
        });
        On<ServiceIdentityRetired>(fact =>
        {
            RequireNamedFact(fact.TenantId, fact.ServiceIdentityId, fact.Revision);
            if (fact.Reason is { Length: > 1000 })
                throw new InvalidOperationException("A retirement fact requires a bounded reason.");
            _terms = _terms! with { LifecycleStatus = "retired" };
            _hasRetired = true;
            _revision = fact.Revision;
        });
    }

    public Result<ServiceIdentityRegistration> Record(ServiceIdentityTerms terms, DateOnly today,
        ActorReference actor, DateTimeOffset changedAt)
    {
        if (_created && terms is { DisplayName: not null, Purpose: not null } &&
            _initialTerms == Clean(terms))
            return Result<ServiceIdentityRegistration>.Success(new ServiceIdentityRegistration(Id));
        var validation = Validate(terms, today);
        if (validation is not null)
            return Result<ServiceIdentityRegistration>.Failure(validation);
        if (_created)
            return Result<ServiceIdentityRegistration>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The service identity already exists with different content."));
        RaiseEvent(new ServiceIdentityRecorded(_tenantId, Id, Clean(terms), actor, changedAt));
        return Result<ServiceIdentityRegistration>.Success(new ServiceIdentityRegistration(Id));
    }

    public CommandFailure? Revise(long expectedRevision, ServiceIdentityTerms terms, DateOnly today,
        ActorReference actor, DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The service identity was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("service identity", _revision));
        var validation = Validate(terms, today);
        if (validation is not null)
            return CommandFailure.InvalidContent(validation.Message!);
        var clean = Clean(terms);
        if (_terms == clean)
            return null;
        if (_hasRetired)
            return CommandFailure.StateConflict("A retired service identity cannot be changed.");
        RaiseEvent(new ServiceIdentityRevised(_tenantId, Id, _revision + 1, clean, actor,
            changedAt));
        return null;
    }

    public CommandFailure? ChangeOwner(long expectedRevision, string ownerKind, Uuid ownerId,
        ActorReference actor, DateTimeOffset changedAt)
    {
        var refusal = RequireNamedChange(expectedRevision);
        if (refusal is not null)
            return refusal;
        var current = _terms!;
        if (!OwnerKinds.Contains(ownerKind) || ownerId == Uuid.Empty ||
            current.IdentityKind == "shared_account" && ownerKind != "person")
            return CommandFailure.InvalidContent("A service identity requires one accountable owner; a shared account requires a person.");
        if (current.OwnerKind == ownerKind && current.OwnerId == ownerId)
            return null;
        RaiseEvent(new ServiceIdentityOwnerChanged(_tenantId, Id, _revision + 1,
            current.OwnerKind, current.OwnerId, ownerKind, ownerId, actor, changedAt));
        return null;
    }

    public CommandFailure? ChangePurpose(long expectedRevision, string purpose,
        ActorReference actor, DateTimeOffset changedAt)
    {
        var refusal = RequireNamedChange(expectedRevision);
        if (refusal is not null)
            return refusal;
        if (string.IsNullOrWhiteSpace(purpose) || purpose.Trim().Length > 1000)
            return CommandFailure.InvalidContent("A service identity requires an approved purpose of at most 1000 characters.");
        var clean = purpose.Trim();
        if (_terms!.Purpose == clean)
            return null;
        RaiseEvent(new ServiceIdentityPurposeChanged(_tenantId, Id, _revision + 1, clean, actor, changedAt));
        return null;
    }

    public CommandFailure? Review(long expectedRevision, DateOnly nextReviewDate, string? notes,
        ActorReference actor, DateTimeOffset changedAt)
    {
        var refusal = RequireNamedChange(expectedRevision);
        if (refusal is not null)
            return refusal;
        var reviewedOn = DateOnly.FromDateTime(changedAt.UtcDateTime);
        if (nextReviewDate <= reviewedOn || nextReviewDate > reviewedOn.AddYears(1))
            return CommandFailure.InvalidContent("The next review date must be in the next year.");
        if (notes?.Trim().Length > 1000)
            return CommandFailure.InvalidContent("Review notes must be at most 1000 characters.");
        RaiseEvent(new ServiceIdentityReviewed(_tenantId, Id, _revision + 1, actor,
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(), nextReviewDate, changedAt));
        return null;
    }

    public CommandFailure? Disable(long expectedRevision, string? reason,
        ActorReference actor, DateTimeOffset changedAt)
    {
        var refusal = RequireNamedChange(expectedRevision);
        if (refusal is not null)
            return refusal;
        if (_terms!.LifecycleStatus != "active")
            return CommandFailure.StateConflict("Only an active service identity can be disabled.");
        if (reason is { Length: > 1000 })
            return CommandFailure.InvalidContent("The disable reason must be at most 1000 characters.");
        RaiseEvent(new ServiceIdentityDisabled(_tenantId, Id, _revision + 1, reason, actor, changedAt));
        return null;
    }

    public CommandFailure? Enable(long expectedRevision,
        ActorReference actor, DateTimeOffset changedAt)
    {
        var refusal = RequireNamedChange(expectedRevision);
        if (refusal is not null)
            return refusal;
        var current = _terms!;
        if (current.LifecycleStatus != "disabled")
            return CommandFailure.StateConflict("Only a disabled service identity can be enabled.");
        if (current.ExpiresOn is { } expiry && expiry <= DateOnly.FromDateTime(changedAt.UtcDateTime))
            return CommandFailure.InvalidContent("An expired service identity cannot be enabled.");
        RaiseEvent(new ServiceIdentityEnabled(_tenantId, Id, _revision + 1, actor, changedAt));
        return null;
    }

    public CommandFailure? Retire(long expectedRevision, string? reason,
        ActorReference actor, DateTimeOffset changedAt)
    {
        var refusal = RequireNamedChange(expectedRevision);
        if (refusal is not null)
            return refusal;
        if (reason is { Length: > 1000 })
            return CommandFailure.InvalidContent("The retirement reason must be at most 1000 characters.");
        RaiseEvent(new ServiceIdentityRetired(_tenantId, Id, _revision + 1, reason, actor, changedAt));
        return null;
    }

    CommandFailure? RequireNamedChange(long expectedRevision)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The service identity was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("service identity", _revision));
        return _hasRetired
            ? CommandFailure.StateConflict("A retired service identity cannot be changed.")
            : null;
    }

    void RequireNamedFact(Uuid tenantId, Uuid identityId, long revision)
    {
        if (tenantId != _tenantId || identityId != Id || !_created || _hasRetired || revision != _revision + 1)
            throw new InvalidOperationException("A named service identity fact must follow its exact non-retired predecessor.");
    }

    static RequestError? Validate(ServiceIdentityTerms terms, DateOnly today)
    {
        static RequestError Invalid(string message) => new(RequestErrorKind.Validation, message);
        if (terms is null || string.IsNullOrWhiteSpace(terms.DisplayName) ||
            terms.DisplayName.Trim().Length > 200)
            return Invalid("A service identity requires a display name of at most 200 characters.");
        if (!IdentityKinds.Contains(terms.IdentityKind))
            return Invalid(
                "The identity kind must be workload, service, automation, bot, integration, or shared_account.");
        if (string.IsNullOrWhiteSpace(terms.Purpose) || terms.Purpose.Trim().Length > 1000)
            return Invalid("A service identity requires an approved purpose of at most 1000 characters.");
        if (terms.Environment is { } environment && environment.Trim().Length > 100)
            return Invalid("The environment must be at most 100 characters.");
        if (!LifecycleStatuses.Contains(terms.LifecycleStatus))
            return Invalid("The lifecycle status must be active, disabled, or retired.");
        if (!OwnerKinds.Contains(terms.OwnerKind) || terms.OwnerId == Uuid.Empty)
            return Invalid("A service identity requires exactly one accountable person or team owner.");
        if (terms.IdentityKind == "shared_account" && (terms.OwnerKind != "person" ||
            string.IsNullOrWhiteSpace(terms.SharedJustification) || terms.SharedJustification.Trim().Length > 1000))
            return Invalid("A shared account requires an accountable person and a justification of at most 1000 characters.");
        if (terms.ReviewBy > today.AddYears(1))
            return Invalid("The review date must be at most one year out.");
        if (terms.LifecycleStatus != "retired" && terms.ReviewBy <= today)
            return Invalid("The review date must be in the future.");
        if (terms.LifecycleStatus == "active" && terms.ExpiresOn is { } expiresOn &&
            expiresOn <= today)
            return Invalid("An active service identity cannot already be expired.");
        return null;
    }

    static ServiceIdentityTerms Clean(ServiceIdentityTerms terms) => terms with
    {
        DisplayName = terms.DisplayName.Trim(),
        Purpose = terms.Purpose.Trim(),
        Environment = string.IsNullOrWhiteSpace(terms.Environment) ? null : terms.Environment.Trim(),
        SharedJustification = string.IsNullOrWhiteSpace(terms.SharedJustification) ? null : terms.SharedJustification.Trim(),
    };

    void ObserveRecorded(ServiceIdentityRecorded fact)
    {
        if (fact.TenantId != _tenantId || fact.ServiceIdentityId != Id || _created)
            throw new InvalidOperationException("A service identity record must create this tenant's identity exactly once.");
        var validation = Validate(fact.Terms, DateOnly.FromDateTime(fact.ChangedAt.UtcDateTime));
        if (validation is not null || fact.Terms != Clean(fact.Terms))
            throw new InvalidOperationException("A service identity record must contain valid canonical terms.");
        _created = true;
        _revision = 1;
        _initialTerms = fact.Terms;
        _terms = fact.Terms;
        _hasRetired |= fact.Terms.LifecycleStatus == "retired";
    }

    void ObserveRevised(ServiceIdentityRevised fact)
    {
        if (fact.TenantId != _tenantId || fact.ServiceIdentityId != Id || !_created ||
            fact.Revision != _revision + 1)
            throw new InvalidOperationException("A service identity revision must follow its tenant's current identity revision.");
        var validation = Validate(fact.Terms, DateOnly.FromDateTime(fact.ChangedAt.UtcDateTime));
        if (validation is not null || fact.Terms != Clean(fact.Terms))
            throw new InvalidOperationException("A service identity revision must contain valid canonical terms.");
        _revision = fact.Revision;
        _terms = fact.Terms;
        _hasRetired |= fact.Terms.LifecycleStatus == "retired";
    }
}
