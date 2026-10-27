using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.History;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Rules;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
namespace Bdgrz.Compliance.Features.Work.Aggregates;

/// <summary>Bounded latest/explicit selected member-week authority; complete dispatch history is external.</summary>
public sealed class WorkDigestDispatch : Aggregate
{
    public const string Scheduled = "scheduled";
    public const string InFlight = "in_flight";
    public const string RetryPending = "retry_pending";
    public const string Sent = "sent";
    public const string Skipped = "skipped";
    public const string PermanentFailure = "permanent_failure";
    public const string RetryExhausted = "retry_exhausted";
    public const string Unknown = "unknown";

    public const string Area = "work-digest-dispatches";
    readonly Uuid _tenant;
    readonly DateOnly _selectedWeek;
    WorkDigestDispatchState? _latest;
    WorkDigestDispatchState? _selected;
    public WorkDigestDispatch(Uuid tenantId, Uuid memberId, DateOnly selectedWeek = default, Uuid selectedCommandId = default)
        : base(memberId, new EventStreamAddress(tenantId.ToString(), Area, memberId.ToString()))
    {
        (_tenant, _selectedWeek) = (tenantId, selectedWeek);
        Acceptance = new NativeAcceptance(selectedCommandId, nameof(AuthorizeWorkDigestUnknownRetry));
        On<WorkDigestDispatchScheduled>(ApplyFact);
        On<WorkDigestDispatchAttemptStarted>(ApplyFact);
        On<WorkDigestDispatchOutcomeRecorded>(ApplyFact);
        On<WorkDigestUnknownRetryAuthorized>(ApplyFact);
    }
    public Uuid TenantId => _tenant;
    public NativeAcceptance Acceptance { get; }
    public long Revision => checked((long)Version);
    public DateOnly SelectedWeek => _selectedWeek;
    internal WorkDigestDispatchState? LatestState => _latest;
    internal WorkDigestDispatchState? SelectedState => _selectedWeek == _latest?.WeekOf ? _latest : _selected;
    public DomainEvent? Latest { get; private set; }
    internal WorkDigestReplayCut ReplayCut { get; } = new();
    internal bool Schedule(WorkDigestScheduleWindow window, Uuid messageId, TimeSpan retryWindow, DateTimeOffset recordedAt)
    {
        if (_latest is { } latest && latest.WeekOf >= window.WeekOf || !ReplayCut.Allows(this))
            return false;
        var rules = new WorkDigestDispatchRules(_tenant, Id);
        if (!rules.Schedule(window, messageId, retryWindow, recordedAt))
            return false;
        RaiseEvent(rules.PendingEvents.Single());
        return true;
    }
    WorkDigestDispatchState? State(DateOnly week)
    {
        if (_latest?.WeekOf == week)
            return _latest;
        if (_selectedWeek == week)
            return _selected;
        if (_latest is not null && week < _latest.WeekOf)
            throw new InvalidOperationException("Historical dispatch authority requires explicit selected-week hydration.");
        return null;
    }
    public WorkDigestDispatchStatusView Read(DateOnly week) => (State(week)?.View(_tenant, Id) ??
        new WorkDigestDispatchStatusView(_tenant, Id, week, "UTC", DateTimeOffset.MinValue, "not_scheduled", 0,
            null, Uuid.Empty, DateTimeOffset.MinValue, null, null, null, null, null)) with
    { Revision = Revision };
    internal DateTimeOffset RetryDeadline(DateOnly week) => State(week)?.RetryDeadline ?? DateTimeOffset.MinValue;
    bool Change(DateOnly week, Func<WorkDigestDispatchRules, bool> change)
    {
        if (!ReplayCut.Allows(this) || State(week) is not { } state)
            return false;
        var rules = new WorkDigestDispatchRules(_tenant, Id);
        rules.Restore(state);
        if (!change(rules))
            return false;
        RaiseEvent(rules.PendingEvents.Single());
        return true;
    }
    public bool StartAttempt(DateOnly week, DateTimeOffset at) => Change(week, rules => rules.StartAttempt(week, at));
    public bool RecordSent(DateOnly week, DateTimeOffset at) => Change(week, rules => rules.RecordSent(week, at));
    public bool RecordSkipped(DateOnly week, DateTimeOffset at, string reason) => Change(week, rules => rules.RecordSkipped(week, at, reason));
    public bool RecordPermanentRejection(DateOnly week, DateTimeOffset at, string code) => Change(week, rules => rules.RecordPermanentRejection(week, at, code));
    public bool RecordUnknown(DateOnly week, DateTimeOffset at, string code) => Change(week, rules => rules.RecordUnknown(week, at, code));
    public bool RecordInterruptedAttempt(DateOnly week, DateTimeOffset at) => Change(week, rules => rules.RecordInterruptedAttempt(week, at));
    internal Result<WorkDigestRetryPreparation> PrepareRetry(AuthorizeWorkDigestUnknownRetry request,
        OperationProvenance operation, int maximumAttempts)
    {
        if (!ReplayCut.Allows(this))
            return Result<WorkDigestRetryPreparation>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact dispatch source cut is not finalized; retry.", true));
        if (request.ExpectedRevision != Revision)
            return Result<WorkDigestRetryPreparation>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The member dispatch revision is {Revision}."));
        if (request.TenantId != _tenant || request.MemberId != Id || request.WeekOf != _selectedWeek ||
            operation.Actor.Kind != "platform_operator" || operation.FirstOwner != Stream ||
            operation.Actor.Id == string.Empty || State(request.WeekOf) is not { } before)
            return Result<WorkDigestRetryPreparation>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The dispatch is not eligible for a same-week operator-authorized retry."));
        var rules = new WorkDigestDispatchRules(_tenant, Id);
        rules.Restore(before);
        if (!rules.AuthorizeUnknownRetry(request.WeekOf,
                Uuid.Parse(operation.Actor.Id, global::System.Globalization.CultureInfo.InvariantCulture),
                request.EvidenceReference, request.Rationale, operation.AcceptedAt, maximumAttempts))
            return Result<WorkDigestRetryPreparation>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The dispatch is not eligible for a same-week operator-authorized retry."));
        return Result<WorkDigestRetryPreparation>.Success(new WorkDigestRetryPreparation(Version, CommittedStreamPosition,
            before.View(_tenant, Id) with { Revision = Revision }, before.RetryDeadline,
            rules.Read(request.WeekOf) with { Revision = Revision + 1 }));
    }
    internal Result AppendRetry(AuthorizeWorkDigestUnknownRetry request, OperationProvenance operation,
        WorkDigestRetryPreparation prepared, int maximumAttempts, ActorAttribution originalActor, ImmutableContentRef change, ImmutableContentRef body,
        ImmutableContentRef firstResponse, ImmutableContentRef result)
    {
        if (!ReplayCut.Allows(this) || Version != prepared.Revision || CommittedStreamPosition != prepared.PhysicalPosition)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact dispatch authority changed while its retry was prepared.", true));
        var checkedPreparation = PrepareRetry(request, operation, maximumAttempts);
        if (!checkedPreparation.IsSuccess)
            return Result.Failure(checkedPreparation.Error!);
        if (checkedPreparation.Value != prepared || request.CommandId != operation.LogicalId ||
            Acceptance.SelectedLogicalId != operation.LogicalId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The retry differs from its exact prepared command and dispatch cut."));
        var authorizedBy = Uuid.Parse(operation.Actor.Id, global::System.Globalization.CultureInfo.InvariantCulture);
        var fact = new WorkDigestUnknownRetryAuthorized(_tenant, Id, request.WeekOf, prepared.Before.MessageId,
            prepared.Before.Attempts, authorizedBy, request.EvidenceReference.Trim(), request.Rationale.Trim(), operation.AcceptedAt)
        {
            Operation = operation,
            Authored = request,
            OriginalActor = originalActor,
            Revision = Version + 1,
            MaximumAttempts = maximumAttempts,
            Change = change,
            ResponseContent = body,
            FirstResponse = firstResponse,
            OperationResult = result
        };
        WorkDigestRetryContent.Validate(fact, this, Version, State(request.WeekOf));
        if (DomainEventPayload.Size(fact) > DomainEventPayload.MaximumBytes)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The native retry authorization exceeds the persisted business frame limit."));
        RaiseEvent(fact);
        return Result.Success;
    }
    public bool RecordTransientRejection(DateOnly week, DateTimeOffset at, int maximumAttempts, TimeSpan delay) =>
        Change(week, rules => rules.RecordTransientRejection(week, at, maximumAttempts, delay));
    public bool RecordRetryWindowExpired(DateOnly week, DateTimeOffset at) => Change(week, rules => rules.RecordRetryWindowExpired(week, at));
    public bool RecordMissedWeek(DateOnly week, DateTimeOffset at) => Change(week, rules => rules.RecordMissedWeek(week, at));
    void ApplyFact(DomainEvent fact)
    {
        var (tenant, member, week) = WorkDigestDispatchHistory.Scope(fact);
        if (tenant != _tenant || member != Id)
            throw new ImmutableContentIntegrityException("The dispatch fact belongs to another member or tenant.");
        if (fact is WorkDigestUnknownRetryAuthorized authorization)
        {
            var before = _latest?.WeekOf == week ? _latest : _selectedWeek == week ? _selected : null;
            WorkDigestRetryContent.Validate(authorization, this, Version, before);
            try
            { Acceptance.Observe(authorization); }
            catch (InvalidOperationException exception)
            {
                throw new ImmutableContentIntegrityException("The dispatch repeats a selected native authorization: " + exception.Message);
            }
        }
        if (fact is WorkDigestDispatchScheduled scheduled)
        {
            if (_latest is { } latest && week <= latest.WeekOf)
                throw new ImmutableContentIntegrityException("A dispatch schedule cannot repeat or precede its latest week.");
            var rules = new WorkDigestDispatchRules(_tenant, Id);
            rules.Replay(scheduled);
            _latest = rules.States[week];
            if (week == _selectedWeek)
                _selected = _latest;
        }
        else
        {
            if (_latest?.WeekOf == week)
                _latest = Updated(_latest, fact);
            if (_selectedWeek == week && _selected is not null && _latest?.WeekOf != week)
                _selected = Updated(_selected, fact);
            else if (_selectedWeek == week && _latest?.WeekOf == week)
                _selected = _latest;
        }
        Latest = fact;
        ReplayCut.Observe(fact);
    }
    WorkDigestDispatchState Updated(WorkDigestDispatchState state, DomainEvent fact)
    {
        var rules = new WorkDigestDispatchRules(_tenant, Id);
        rules.Restore(state);
        rules.Replay(fact);
        return rules.States[state.WeekOf];
    }
}
