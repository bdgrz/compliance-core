using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Rules;
namespace Bdgrz.Compliance.Features.Workforce.Aggregates;

/// <summary>
///     Owns one person's work relationship, keyed by the tenant and the stable source worker ID so
///     each worker ID identifies exactly one relationship (M0-D06). Email is never an identifier.
/// </summary>
public sealed class WorkRelationship : Aggregate
{
    static readonly string[] WorkerTypes = ["employee", "contractor", "external_collaborator"];
    static readonly string[] LifecycleStatuses = ["pending", "active", "on_leave", "ended"];

    readonly Uuid _tenantId;
    bool _created;
    bool _hasEnded;
    long _revision;
    Uuid _personId;
    string? _sourceWorkerId;
    WorkRelationshipTerms? _initialTerms;
    WorkforceSourceIdentity? _initialSource;
    WorkRelationshipTerms? _terms;

    public bool IsCreated => _created;
    public long Revision => _revision;
    public Uuid PersonId => _personId;

    public WorkRelationship(Uuid tenantId, Uuid relationshipId)
        : base(relationshipId, new EventStreamAddress(tenantId.ToString(), "work-relationships",
            relationshipId.ToString()))
    {
        _tenantId = tenantId;
        On<WorkRelationshipRecorded>(ev =>
        {
            _created = true;
            _revision = 1;
            _personId = ev.PersonId;
            _sourceWorkerId = ev.SourceWorkerId;
            _initialTerms = ev.Terms;
            _initialSource = ev.Source;
            _terms = ev.Terms;
            _hasEnded |= ev.Terms is { LifecycleStatus: "ended" };
        });
        On<WorkRelationshipRevised>(ev =>
        {
            _revision = ev.Revision;
            _terms = ev.Terms;
            _hasEnded |= ev.Terms is { LifecycleStatus: "ended" };
        });
        On<WorkRelationshipStarted>(ev =>
        {
            _revision = ev.Revision;
            _terms = _terms! with { LifecycleStatus = "active", StartDate = ev.StartDate };
        });
        On<WorkRelationshipLeaveStarted>(ev =>
        {
            _revision = ev.Revision;
            _terms = _terms! with { LifecycleStatus = "on_leave" };
        });
        On<WorkRelationshipLeaveEnded>(ev =>
        {
            _revision = ev.Revision;
            _terms = _terms! with { LifecycleStatus = "active" };
        });
        On<WorkRelationshipEnded>(ev =>
        {
            _revision = ev.Revision;
            _hasEnded = true;
            _terms = _terms! with
            {
                LifecycleStatus = "ended",
                EndDate = ev.EndDate,
                EmploymentStatusReason = ev.Reason,
            };
        });
        On<WorkRelationshipManagerChanged>(ev =>
        {
            _revision = ev.Revision;
            _terms = _terms! with { ManagerPersonId = ev.ManagerPersonId };
        });
        On<WorkRelationshipDepartmentChanged>(ev =>
        {
            _revision = ev.Revision;
            _terms = _terms! with { Department = ev.Department };
        });
        On<WorkRelationshipSponsorChanged>(ev =>
        {
            _revision = ev.Revision;
            _terms = _terms! with { SponsorPersonId = ev.SponsorPersonId };
        });
        On<WorkRelationshipDatesCorrected>(ev =>
        {
            _revision = ev.Revision;
            _terms = _terms! with { StartDate = ev.StartDate, EndDate = ev.EndDate };
        });
    }

    public static string NormalizeWorkerId(string? sourceWorkerId) =>
        sourceWorkerId?.Trim().ToUpperInvariant() ?? string.Empty;

    public static Uuid IdFor(Uuid tenantId, string sourceWorkerId) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, "work-relationships"),
            NormalizeWorkerId(sourceWorkerId));

    public Result<WorkRelationshipRegistration> Record(Uuid personId, string sourceWorkerId,
        WorkRelationshipTerms terms, ActorReference actor, DateTimeOffset changedAt,
        WorkforceSourceIdentity? source = null)
    {
        var workerId = NormalizeWorkerId(sourceWorkerId);
        if (workerId.Length is 0 or > 100)
            return Result<WorkRelationshipRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, "A source worker ID of 1 to 100 characters is required."));
        var validation = Validate(personId, terms);
        if (validation is not null)
            return Result<WorkRelationshipRegistration>.Failure(validation);
        var clean = Clean(terms);
        var cleanSource = source is null ? null : WorkforceSourceIdentityRules.Normalize(source);
        if (cleanSource is not null)
        {
            var sourceError = WorkforceSourceIdentityRules.InputError(cleanSource);
            if (sourceError is not null)
                return Result<WorkRelationshipRegistration>.Failure(new RequestError(RequestErrorKind.Validation, sourceError));
            if (cleanSource.SourceKind is not ("hris" or "idp"))
                return Result<WorkRelationshipRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                    "Work relationship provenance must name an HRIS or identity-provider source."));
        }
        if (_created)
            return _personId == personId && _sourceWorkerId == workerId && _initialTerms == clean && _initialSource == cleanSource
                ? Result<WorkRelationshipRegistration>.Success(new WorkRelationshipRegistration(Id))
                : Result<WorkRelationshipRegistration>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The source worker ID already identifies a different work relationship."));
        if (clean.LifecycleStatus is not ("pending" or "active"))
            return Result<WorkRelationshipRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "A new work relationship must be pending or active."));
        RaiseEvent(new WorkRelationshipRecorded(_tenantId, Id, personId, workerId, clean, actor,
            changedAt, cleanSource));
        return Result<WorkRelationshipRegistration>.Success(new WorkRelationshipRegistration(Id));
    }

    public CommandFailure? Revise(long expectedRevision, WorkRelationshipTerms terms,
        ActorReference actor, DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The work relationship was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("work relationship", _revision));
        var validation = Validate(_personId, terms);
        if (validation is not null)
            return CommandFailure.InvalidContent(validation.Message!);
        if (_hasEnded && terms.LifecycleStatus != "ended")
            return CommandFailure.StateConflict(
                "An ended work relationship must stay ended; record a new relationship for a rehire.");
        RaiseEvent(new WorkRelationshipRevised(_tenantId, Id, _revision + 1, Clean(terms), actor,
            changedAt));
        return null;
    }

    public CommandFailure? Start(long expectedRevision, DateOnly startDate,
        ActorReference actor, DateTimeOffset changedAt)
    {
        var refusal = RequireRevision(expectedRevision);
        if (refusal is not null)
            return refusal;
        if (_hasEnded || _terms!.LifecycleStatus != "pending")
            return CommandFailure.StateConflict("Only a pending work relationship can be started.");
        var invalid = Validate(_personId, _terms! with { LifecycleStatus = "active", StartDate = startDate });
        if (invalid is not null)
            return CommandFailure.InvalidContent(invalid.Message!);
        RaiseEvent(new WorkRelationshipStarted(_tenantId, Id, _sourceWorkerId!, _revision + 1,
            startDate, actor, changedAt));
        return null;
    }

    public CommandFailure? StartLeave(long expectedRevision, DateOnly fromDate,
        ActorReference actor, DateTimeOffset changedAt)
    {
        var refusal = RequireRevision(expectedRevision);
        if (refusal is not null)
            return refusal;
        if (_hasEnded || _terms!.LifecycleStatus != "active")
            return CommandFailure.StateConflict("Only an active work relationship can start leave.");
        RaiseEvent(new WorkRelationshipLeaveStarted(_tenantId, Id, _sourceWorkerId!, _revision + 1,
            fromDate, actor, changedAt));
        return null;
    }

    public CommandFailure? EndLeave(long expectedRevision, DateOnly returnDate,
        ActorReference actor, DateTimeOffset changedAt)
    {
        var refusal = RequireRevision(expectedRevision);
        if (refusal is not null)
            return refusal;
        if (_hasEnded || _terms!.LifecycleStatus != "on_leave")
            return CommandFailure.StateConflict("Only a work relationship on leave can return from leave.");
        RaiseEvent(new WorkRelationshipLeaveEnded(_tenantId, Id, _sourceWorkerId!, _revision + 1,
            returnDate, actor, changedAt));
        return null;
    }

    public CommandFailure? End(long expectedRevision, DateOnly endDate, string? reason,
        ActorReference actor, DateTimeOffset changedAt)
    {
        var refusal = RequireRevision(expectedRevision);
        if (refusal is not null)
            return refusal;
        if (_hasEnded || _terms!.LifecycleStatus == "ended")
            return CommandFailure.StateConflict("An ended work relationship stays ended.");
        if (endDate < _terms!.StartDate)
            return CommandFailure.InvalidContent("The end date cannot be before the start date.");
        RaiseEvent(new WorkRelationshipEnded(_tenantId, Id, _sourceWorkerId!, _revision + 1,
            endDate, reason, actor, changedAt));
        return null;
    }

    CommandFailure? RequireRevision(long expectedRevision)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The work relationship was not found.");
        return expectedRevision != _revision
            ? CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("work relationship", _revision))
            : null;
    }

    public CommandFailure? ChangeManager(long expectedRevision, Uuid? managerPersonId,
        ActorReference actor, DateTimeOffset changedAt)
    {
        var refusal = RequireRevision(expectedRevision);
        if (refusal is not null)
            return refusal;
        if (_hasEnded)
            return CommandFailure.StateConflict("An ended work relationship cannot change its manager.");
        if (managerPersonId == _personId || managerPersonId == Uuid.Empty)
            return CommandFailure.InvalidContent("The manager must be another recorded person or absent.");
        if (_terms!.ManagerPersonId == managerPersonId)
            return null;
        RaiseEvent(new WorkRelationshipManagerChanged(_tenantId, Id, _sourceWorkerId!, _revision + 1,
            managerPersonId, actor, changedAt));
        return null;
    }

    public CommandFailure? ChangeDepartment(long expectedRevision, string? department,
        ActorReference actor, DateTimeOffset changedAt)
    {
        var refusal = RequireRevision(expectedRevision);
        if (refusal is not null)
            return refusal;
        if (_hasEnded)
            return CommandFailure.StateConflict("An ended work relationship cannot change its department.");
        var clean = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
        if (clean is { Length: > 200 })
            return CommandFailure.InvalidContent("The department must be at most 200 characters.");
        if (_terms!.Department == clean)
            return null;
        RaiseEvent(new WorkRelationshipDepartmentChanged(_tenantId, Id, _sourceWorkerId!, _revision + 1,
            clean, actor, changedAt));
        return null;
    }

    public CommandFailure? ChangeSponsor(long expectedRevision, Uuid sponsorPersonId,
        ActorReference actor, DateTimeOffset changedAt)
    {
        var refusal = RequireRevision(expectedRevision);
        if (refusal is not null)
            return refusal;
        if (_terms!.WorkerType != "external_collaborator")
            return CommandFailure.StateConflict("Only an external collaborator has an internal sponsor.");
        if (sponsorPersonId == _personId || sponsorPersonId == Uuid.Empty)
            return CommandFailure.InvalidContent("The sponsor must be another recorded person.");
        if (_terms!.SponsorPersonId == sponsorPersonId)
            return null;
        RaiseEvent(new WorkRelationshipSponsorChanged(_tenantId, Id, _sourceWorkerId!, _revision + 1,
            sponsorPersonId, actor, changedAt));
        return null;
    }

    public CommandFailure? CorrectDates(long expectedRevision, DateOnly startDate, DateOnly? endDate,
        string reason, ActorReference actor, DateTimeOffset changedAt)
    {
        var refusal = RequireRevision(expectedRevision);
        if (refusal is not null)
            return refusal;
        if (string.IsNullOrWhiteSpace(reason))
            return CommandFailure.InvalidContent("A date correction requires a reason.");
        if (_terms!.LifecycleStatus == "ended" && endDate is null)
            return CommandFailure.InvalidContent("An ended work relationship requires an end date.");
        if (endDate is { } end && end < startDate)
            return CommandFailure.InvalidContent("The end date cannot be before the start date.");
        if (_terms!.StartDate == startDate && _terms!.EndDate == endDate)
            return null;
        RaiseEvent(new WorkRelationshipDatesCorrected(_tenantId, Id, _sourceWorkerId!, _revision + 1,
            startDate, endDate, reason.Trim(), actor, changedAt));
        return null;
    }

    static RequestError? Validate(Uuid personId, WorkRelationshipTerms terms)
    {
        static RequestError Invalid(string message) => new(RequestErrorKind.Validation, message);
        if (personId == Uuid.Empty)
            return Invalid("A work relationship requires a person.");
        if (terms is null || !WorkerTypes.Contains(terms.WorkerType))
            return Invalid("The worker type must be employee, contractor, or external_collaborator.");
        if (!LifecycleStatuses.Contains(terms.LifecycleStatus))
            return Invalid("The lifecycle status must be pending, active, on_leave, or ended.");
        if (terms.LifecycleStatus == "ended" && terms.EndDate is null)
            return Invalid("An ended work relationship requires an end date.");
        if (terms.EndDate is { } end && end < terms.StartDate)
            return Invalid("The end date cannot be before the start date.");
        if (terms.Department is { } department && department.Trim().Length > 200)
            return Invalid("The department must be at most 200 characters.");
        if (terms.ManagerPersonId == personId || terms.SponsorPersonId == personId)
            return Invalid("A person cannot be their own manager or sponsor.");
        if (terms.WorkerType == "external_collaborator" && terms.SponsorPersonId is null)
            return Invalid("An external collaborator requires an accountable internal sponsor.");
        return null;
    }

    static WorkRelationshipTerms Clean(WorkRelationshipTerms terms) => terms with
    {
        Department = string.IsNullOrWhiteSpace(terms.Department) ? null : terms.Department.Trim(),
    };
}
