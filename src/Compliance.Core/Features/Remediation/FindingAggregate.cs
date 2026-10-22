using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using System.Text.Json;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     One finding's bounded command state. Its stream is tenant and program scoped; audit history,
///     linked acceptances, and prior closures are reconstructed by read-side folds.
/// </summary>
public sealed partial class FindingAggregate : Aggregate
{
    public const string Area = "findings";
    internal const string LegacySourceArea = "remediation";
    public const string Open = FindingStates.Open;
    public const string Remediated = FindingStates.Remediated;
    public const string Closed = FindingStates.Closed;
    public const string Accepted = FindingStates.Accepted;
    public const string Overdue = FindingStates.Overdue;
    public const string Unresolved = FindingStates.Unresolved;
    public const string Completed = FindingStates.Completed;
    public const string RiskAcceptance = FindingStates.RiskAcceptance;
    public const string Waiver = FindingStates.Waiver;
    const int MaximumTextLength = 4000;
    const int MaximumEventPayloadBytes = 48 * 1024;

    static readonly string[] SourceKinds = ["manual", "readiness_gap", "control_occurrence",
        "occurrence_review", "evaluation_deviation", "incident", "consultant_observation",
        "examination_item"];
    static readonly string[] LinkKinds = ["control", "criterion", "evidence", "access_decision",
        "risk", "vendor", "readiness_gap", "control_occurrence", "control_evaluation"];
    static readonly string[] Severities = ["low", "medium", "high", "critical"];

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _findingId;
    readonly List<CorrectiveActionView> _actions = [];
    FindingRaised? _raised;
    long _revision;
    string? _severity;
    Uuid _ownerMemberId;
    DateOnly _dueOn;
    string? _affectedScope;
    string? _rootCause;
    FindingClosureView? _closure;
    FindingHistoryEventImported? _lastImported;
    FindingHistoryEventImported? _pendingImport;

    public FindingAggregate(Uuid tenantId, Uuid programId, Uuid findingId, Uuid? selectedRequestId = null)
        : base(StreamId(programId, findingId), new EventStreamAddress(tenantId.ToString(), Area,
            StreamId(programId, findingId).ToString()))
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || findingId == Uuid.Empty)
            throw new ArgumentException("A finding stream needs tenant, program, and finding IDs.");

        _tenantId = tenantId;
        _programId = programId;
        _findingId = findingId;
        _selectedRequestId = selectedRequestId;

        On<FindingRaised>(Apply);
        On<FindingRaisedV2>(ApplyNative);
        On<FindingRevisedV2>(ApplyNative);
        On<CorrectiveActionAddedV2>(ApplyNative);
        On<CorrectiveActionCompletedV2>(ApplyNative);
        On<FindingAcceptanceLinkedV2>(ApplyNative);
        On<FindingClosedV2>(ApplyNative);
        On<FindingReopenedV2>(ApplyNative);
        On<FindingRevised>(Apply);
        On<CorrectiveActionAdded>(Apply);
        On<CorrectiveActionCompleted>(Apply);
        On<FindingAcceptanceLinked>(Apply);
        On<FindingClosed>(Apply);
        On<FindingReopened>(Apply);
        On<FindingHistoryEventImported>(ApplyImportedMarker);
    }

    public static Uuid StreamId(Uuid programId, Uuid findingId)
    {
        if (programId == Uuid.Empty || findingId == Uuid.Empty)
            throw new ArgumentException("A finding stream needs program and finding IDs.");
        return Uuid.CreateVersion5(programId, $"finding:{findingId}");
    }

    public bool IsRecorded => _raised is not null;
    public long Revision => _revision;
    public string? Severity => _severity;
    public Uuid OwnerMemberId => _ownerMemberId;
    public DateOnly DueOn => _dueOn;
    public string? AffectedScope => _affectedScope;
    public string? RootCause => _rootCause;
    public IReadOnlyList<CorrectiveActionView> CorrectiveActions => _actions.AsReadOnly();
    public FindingClosureView? Closure => _closure;

    public CommandFailure? Raise(Uuid requestId, FindingSource source, string title, string description,
        string severity, string affectedScope, Uuid ownerMemberId, DateOnly dueOn,
        IReadOnlyList<FindingLink> links, ActorReference raisedBy, DateTimeOffset raisedAt) =>
        RaiseCore(requestId, source, title, description, severity, affectedScope, ownerMemberId,
            dueOn, links, raisedBy, raisedAt, "manual", null);

    CommandFailure? RaiseCore(Uuid requestId, FindingSource source, string title, string description,
        string severity, string affectedScope, Uuid ownerMemberId, DateOnly dueOn,
        IReadOnlyList<FindingLink> links, ActorReference raisedBy, DateTimeOffset raisedAt,
        string creationKind, CorrectiveActionAddedV2? initialAction)
    {
        EnsureImportComplete();
        ArgumentNullException.ThrowIfNull(links);
        if (CheckRequest(requestId) is { } requestFailure)
            return requestFailure;
        if (_selectedReceipt is not null)
            return RetryRaise(source, title, description, severity, affectedScope, ownerMemberId,
                dueOn, links, raisedBy, creationKind, initialAction);
        if (IsRecorded)
            return ChangedRequest();
        if (source is null || !SourceKinds.Contains(source.Kind) ||
            string.IsNullOrWhiteSpace(source.SourceText) || source.SourceText.Length > 8000 ||
            source.Version is { Length: > 200 } ||
            source.Kind is not ("manual" or "incident" or "consultant_observation") &&
            source.RecordId is null)
            return CommandFailure.InvalidContent(
                "A finding source needs a known kind, the source record for a governed source, and source text of at most 8000 characters.");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200 ||
            !IsBoundedText(description, 8000) || !IsBoundedText(affectedScope, 2000))
            return CommandFailure.InvalidContent(
                "A finding needs a title of at most 200 characters, a description, and an affected scope.");
        if (ValidateTriage(severity, ownerMemberId, dueOn) is { } triage)
            return triage;
        if (links.Count > 50 || links.Any(static link => link is null ||
                !LinkKinds.Contains(link.Kind) || string.IsNullOrWhiteSpace(link.Reference) ||
                link.Reference.Length > 500))
            return CommandFailure.InvalidContent(
                "Each finding link needs a known kind and a reference of at most 500 characters.");

        var ev = new FindingRaisedV2(requestId, _tenantId, _programId, _findingId, 1, source, title.Trim(),
            description.Trim(), severity, affectedScope.Trim(), ownerMemberId, dueOn,
            links.Select(static link => link with { Reference = link.Reference.Trim() }).ToArray(),
            raisedBy, raisedAt, creationKind, initialAction is null ? null : FindingEventFacts.Hash(initialAction));
        if (!FitsEventPayload(ev, ComplianceCoreJsonContext.Default.FindingRaisedV2))
            return EventTooLarge();
        return EmitNative(ev);
    }

    /// <summary>Imports one lineage-checked legacy fact while a program is fenced for migration.</summary>
    internal CommandFailure? ImportLegacy(DomainEventRecord sourceRecord)
    {
        EnsureImportComplete();
        ArgumentNullException.ThrowIfNull(sourceRecord);
        var sourceEvent = sourceRecord.Event;
        if (sourceRecord.ResourceOffset == ulong.MaxValue)
            return CommandFailure.InvalidContent("The legacy finding event lineage is invalid.");
        DomainEventMetadata metadata;
        try
        {
            metadata = sourceEvent.Metadata;
        }
        catch (InvalidOperationException)
        {
            return CommandFailure.InvalidContent("The legacy finding event lineage is invalid.");
        }
        var expectedSourceStream = new EventStreamAddress(_tenantId.ToString(),
            LegacySourceArea, _programId.ToString());
        if (sourceRecord.Stream != expectedSourceStream || metadata.AggregateId != _programId ||
            metadata.AggregateVersion == 0 ||
            metadata.AggregateVersion > sourceRecord.ResourceOffset + 1 ||
            metadata.EventId == Uuid.Empty || metadata.OccurredOn == default ||
            metadata.OccurredOn.Offset != TimeSpan.Zero || metadata.IsAudit ||
            !FindingHistoryEventPayloads.TryCreate(sourceEvent, _tenantId, _programId,
                _findingId, out var payload))
            return CommandFailure.InvalidContent("The legacy finding event lineage is invalid.");

        var imported = new FindingHistoryEventImported(_tenantId, _programId, _findingId,
            payload.Revision, metadata.EventId, sourceRecord.ResourceOffset,
            metadata.AggregateVersion, payload.Kind,
            FindingHistoryEventPayloads.Hash(payload.Json), metadata.OccurredOn);
        if (_lastImported is { } last &&
            last.SourceProgramStreamVersion == metadata.AggregateVersion)
            return SameImport(last, imported)
                ? null
                : CommandFailure.StateConflict(
                    "The legacy finding stream version was imported with different content.");
        if (_lastImported is { } prior && metadata.AggregateVersion <
            prior.SourceProgramStreamVersion)
            return CommandFailure.StateConflict("Legacy finding events must be imported in source order.");
        if (_lastImported is { } priorOffset && sourceRecord.ResourceOffset <=
            priorOffset.SourceProgramStreamOffset)
            return CommandFailure.StateConflict("Legacy finding events must be imported in source order.");
        if (payload.Revision != _revision + 1)
            return CommandFailure.StateConflict(
                "The legacy finding revision does not follow the imported target history.");
        RaiseEvent(imported);
        RaiseEvent(FindingHistoryEventPayloads.Deserialize(payload.Kind, payload.Json));
        return null;
    }

    /// <summary>Rejects a replay that ended after an import marker without its paired fact.</summary>
    public void EnsureImportComplete()
    {
        if (_pendingCreation is not null)
            throw new InvalidOperationException("The finding stream ends with an incomplete routed creation.");
        if (_pendingImport is not null)
            throw new InvalidOperationException(
                "The finding stream ends with an unpaired legacy-import marker.");
    }

    public CommandFailure? Revise(Uuid requestId, long expectedRevision, string severity, Uuid ownerMemberId,
        DateOnly dueOn, string affectedScope, string? rootCause, string reason,
        ActorReference actor, DateTimeOffset at)
    {
        if (CheckRequest(requestId) is { } requestFailure)
            return requestFailure;
        if (TryRetry(requestId, expectedRevision, out var retryFailure, severity: severity,
                owner: ownerMemberId, due: dueOn, scope: affectedScope, cause: rootCause,
                text: reason, actor: actor))
            return retryFailure;
        if (Writable(expectedRevision) is { } blocked)
            return blocked;
        if (ValidateTriage(severity, ownerMemberId, dueOn) is { } triage)
            return triage;
        if (!IsBoundedText(affectedScope, 2000) || rootCause is { } cause &&
            cause.Trim().Length > MaximumTextLength || !IsBoundedText(reason, MaximumTextLength))
            return CommandFailure.InvalidContent(
                "A finding revision needs an affected scope, a root cause of at most 4000 characters, and a reason.");
        var cleanCause = string.IsNullOrWhiteSpace(rootCause) ? null : rootCause.Trim();
        if (_severity == severity && _ownerMemberId == ownerMemberId && _dueOn == dueOn &&
            _affectedScope == affectedScope.Trim() && _rootCause == cleanCause)
            return CommandFailure.InvalidContent("The finding revision changes nothing.");
        var ev = new FindingRevisedV2(requestId, _tenantId, _programId, _findingId, expectedRevision + 1,
            severity, ownerMemberId, dueOn, affectedScope.Trim(), cleanCause, reason.Trim(), actor,
            at);
        if (!FitsEventPayload(ev, ComplianceCoreJsonContext.Default.FindingRevisedV2))
            return EventTooLarge();
        return EmitNative(ev);
    }

    public CommandFailure? AddAction(Uuid requestId, long expectedRevision, Uuid actionId, string description,
        Uuid ownerMemberId, DateOnly dueOn, ActorReference actor, DateTimeOffset at)
    {
        EnsureImportComplete();
        if (CheckRequest(requestId) is { } requestFailure)
            return requestFailure;
        if (_selectedReceipt is not null)
            return RetryAction(expectedRevision, actionId, description, ownerMemberId, dueOn, actor);
        if (actionId == Uuid.Empty)
            return CommandFailure.InvalidContent("A corrective action needs a nonempty identity.");
        if (Writable(expectedRevision) is { } blocked)
            return blocked;
        if (!IsBoundedText(description, MaximumTextLength) || ownerMemberId == Uuid.Empty ||
            dueOn == default)
            return CommandFailure.InvalidContent(
                "A corrective action needs a description of at most 4000 characters, an owner, and a due date.");
        if (_actions.Count >= 50)
            return CommandFailure.StateConflict("A finding can have at most 50 corrective actions.");
        var ev = new CorrectiveActionAddedV2(requestId, _tenantId, _programId, _findingId,
            expectedRevision + 1, new CorrectiveActionView(actionId, description.Trim(),
                ownerMemberId, dueOn, Open, actor, at));
        if (!FitsEventPayload(ev, ComplianceCoreJsonContext.Default.CorrectiveActionAddedV2))
            return EventTooLarge();
        return EmitNative(ev);
    }

    public CommandFailure? CompleteAction(Uuid requestId, long expectedRevision, Uuid actionId,
        string resolutionNotes, IReadOnlyList<EvidenceReference> evidence, Uuid completerMemberId,
        ActorReference actor, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (CheckRequest(requestId) is { } requestFailure)
            return requestFailure;
        if (_selectedReceipt is not null)
            return RetryCompletion(expectedRevision, actionId, resolutionNotes, evidence, completerMemberId, actor);
        if (Writable(expectedRevision) is { } blocked)
            return blocked;
        if (_actions.Find(action => action.ActionId == actionId) is not { } action)
            return CommandFailure.MissingRecord("The corrective action was not found.");
        if (action.Status == Completed)
            return CommandFailure.StateConflict("The corrective action is already completed.");
        if (!IsBoundedText(resolutionNotes, MaximumTextLength) || EvidenceProblem(evidence) is not null)
            return CommandFailure.InvalidContent(
                "Completing a corrective action needs resolution notes and at least one valid evidence reference.");
        var ev = new CorrectiveActionCompletedV2(requestId, _tenantId, _programId, _findingId,
            expectedRevision + 1, actionId, resolutionNotes.Trim(), AsUnresolved(evidence),
            completerMemberId, actor, at);
        if (!FitsEventPayload(ev, ComplianceCoreJsonContext.Default.CorrectiveActionCompletedV2))
            return EventTooLarge();
        return EmitNative(ev);
    }

    public CommandFailure? LinkAcceptance(Uuid requestId, FindingHistoryFold history, long expectedRevision,
        string kind, Uuid recordId, Uuid? decisionId, DateTimeOffset expiresAt,
        ActorReference actor, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (CheckRequest(requestId) is { } requestFailure)
            return requestFailure;
        if (_selectedReceipt is not null)
            return RetryAcceptance(expectedRevision, kind, recordId, decisionId, actor);
        if (history.StreamVersion != Version)
            return CommandFailure.StateConflict(
                "Finding history changed while validating the acceptance. Reload and retry.");
        if (Writable(expectedRevision) is { } blocked)
            return blocked;
        if (history.HasAcceptance(new FindingAcceptanceIdentity(kind, recordId, decisionId)))
            return CommandFailure.StateConflict("The acceptance is already linked to this finding.");
        if (expiresAt <= at)
            return CommandFailure.StateConflict("An expired acceptance cannot be linked.");
        var ev = new FindingAcceptanceLinkedV2(requestId, _tenantId, _programId, _findingId,
            expectedRevision + 1, new FindingAcceptanceView(kind, recordId, decisionId,
                expiresAt, actor, at));
        if (!FitsEventPayload(ev, ComplianceCoreJsonContext.Default.FindingAcceptanceLinkedV2))
            return EventTooLarge();
        return EmitNative(ev);
    }

    public CommandFailure? Close(Uuid requestId, long expectedRevision, Uuid decisionId,
        string verificationRationale, IReadOnlyList<EvidenceReference> resolutionEvidence,
        string rationale, Uuid closerMemberId, ActorReference actor, DateTimeOffset at,
        SeparationOfDutiesWaiver? waiver)
    {
        ArgumentNullException.ThrowIfNull(resolutionEvidence);
        if (CheckRequest(requestId) is { } requestFailure)
            return requestFailure;
        if (_selectedReceipt is not null)
            return RetryClosure(expectedRevision, decisionId, verificationRationale, resolutionEvidence,
                rationale, closerMemberId, actor, waiver?.Id);
        if (Writable(expectedRevision) is { } blocked)
            return blocked;
        if (_actions.Count == 0 || _actions.Exists(static action => action.Status != Completed))
            return CommandFailure.StateConflict(
                "Closing a finding requires completed corrective work; an acceptance is not remediation.");
        if (EvidenceProblem(resolutionEvidence) is not null ||
            !IsBoundedText(verificationRationale, MaximumTextLength) ||
            !IsBoundedText(rationale, MaximumTextLength))
            return CommandFailure.InvalidContent(
                "Closing a finding requires resolution evidence, a verification rationale, and a closure rationale.");
        var scope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.Finding,
            _findingId, _findingId, expectedRevision, SeparationOfDutiesActions.Approve);
        if (waiver is not null && (waiver.TenantId != _tenantId ||
                                   !waiver.Allows(scope, closerMemberId, at)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and finding revision.");
        var involved = _ownerMemberId == closerMemberId || _actions.Exists(action =>
            action.OwnerMemberId == closerMemberId || action.CompletedByMemberId == closerMemberId);
        if (involved && waiver is null)
            return CommandFailure.ActorProhibited(
                "A finding or corrective action owner cannot verify and close their own remediation.");
        var ev = new FindingClosedV2(requestId, _tenantId, _programId, _findingId, expectedRevision + 1,
            new FindingClosureView(decisionId, verificationRationale.Trim(),
                AsUnresolved(resolutionEvidence), rationale.Trim(), closerMemberId, actor, at,
                waiver?.Id));
        if (!FitsEventPayload(ev, ComplianceCoreJsonContext.Default.FindingClosedV2))
            return EventTooLarge();
        return EmitNative(ev);
    }

    public CommandFailure? Reopen(Uuid requestId, long expectedRevision, string reason, ActorReference actor,
        DateTimeOffset at)
    {
        EnsureImportComplete();
        if (CheckRequest(requestId) is { } requestFailure)
            return requestFailure;
        if (_selectedReceipt is not null)
            return _selectedReceipt is FindingReopenedV2 replay && replay.Revision - 1 == expectedRevision &&
                replay.Reason == reason?.Trim() && SameActor(replay.ReopenedBy, actor) ? null : ChangedRequest();
        if (!IsRecorded)
            return CommandFailure.MissingRecord("The finding was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("finding",
                _revision));
        if (_closure is null)
            return CommandFailure.StateConflict("Only a closed finding can be reopened.");
        if (!IsBoundedText(reason, MaximumTextLength))
            return CommandFailure.InvalidContent("Reopening a finding requires a reason.");
        var ev = new FindingReopenedV2(requestId, _tenantId, _programId, _findingId, expectedRevision + 1,
            reason.Trim(), actor, at);
        if (!FitsEventPayload(ev, ComplianceCoreJsonContext.Default.FindingReopenedV2))
            return EventTooLarge();
        return EmitNative(ev);
    }

    public CommandFailure? CheckHistoryVersion(ulong capturedHistoryVersion) =>
        _pendingImport is null && capturedHistoryVersion == Version
            ? null
            : CommandFailure.StateConflict(
                "Finding history changed while validating the command. Reload and retry.");

    protected override void Apply(DomainEvent ev)
    {
        if (_pendingCreation is not null && ev is not CorrectiveActionAddedV2)
            throw new InvalidOperationException("A routed creation must be followed by its declared action.");
        if (ev is FindingHistoryEventImported)
        {
            base.Apply(ev);
            return;
        }

        if (_pendingImport is { } marker)
        {
            if (!FindingHistoryEventPayloads.TryCreate(ev, _tenantId, _programId, _findingId,
                    out var payload) || payload.Revision != marker.Revision ||
                payload.Kind != marker.SourceEventKind ||
                FindingHistoryEventPayloads.Hash(payload.Json) != marker.SourceEventSha256)
                throw new InvalidOperationException(
                    "The finding-import marker is not followed by its matching source fact.");

            _pendingImport = null;
            base.Apply(ev);
            _lastImported = marker;
            return;
        }

        if (FindingEventFacts.RequestId(ev) is null && _pendingCreation is not null)
            throw new InvalidOperationException("A routed creation must be followed by its declared action.");
        base.Apply(ev);
    }

    public FindingView? Read(FindingHistoryFold history, DateTimeOffset now)
    {
        EnsureImportComplete();
        ArgumentNullException.ThrowIfNull(history);
        if (!IsRecorded || history.StreamVersion != Version || _raised is not { } raised)
            return null;
        var acceptances = history.Acceptances.Select(acceptance => acceptance with
        {
            State = acceptance.ExpiresAt > now ? "active" : "expired",
        }).ToArray();
        var remediated = _actions.Count > 0 &&
                         _actions.TrueForAll(static action => action.Status == Completed);
        var status = _closure is not null ? Closed : remediated ? Remediated : Open;
        var readiness = _closure is not null ? Closed
            : remediated ? Remediated
            : acceptances.Any(static acceptance => acceptance.State == "active") ? Accepted
            : _dueOn < DateOnly.FromDateTime(now.UtcDateTime) ? Overdue
            : Unresolved;
        return new FindingView(_tenantId, _programId, _findingId, _revision, raised.Source,
            raised.Title, raised.Description, _severity!, _affectedScope!, _ownerMemberId,
            _dueOn, _rootCause, status, readiness, raised.Links, _actions.ToArray(), acceptances,
            _closure, history.History, raised.RaisedBy, raised.RaisedAt);
    }

    void Apply(FindingRaised ev)
    {
        EnsureScope(ev.TenantId, ev.ProgramId, ev.FindingId);
        if (_raised is not null)
            throw new InvalidOperationException("A finding stream cannot be raised twice.");
        EnsureNextRevision(ev.Revision);
        _raised = ev with { Links = Array.AsReadOnly(ev.Links.ToArray()) };
        _revision = ev.Revision;
        _severity = ev.Severity;
        _ownerMemberId = ev.OwnerMemberId;
        _dueOn = ev.DueOn;
        _affectedScope = ev.AffectedScope;
    }

    void Apply(FindingRevised ev)
    {
        EnsureScope(ev.TenantId, ev.ProgramId, ev.FindingId);
        EnsureRecorded();
        EnsureNextRevision(ev.Revision);
        _revision = ev.Revision;
        _severity = ev.Severity;
        _ownerMemberId = ev.OwnerMemberId;
        _dueOn = ev.DueOn;
        _affectedScope = ev.AffectedScope;
        _rootCause = ev.RootCause;
    }

    void Apply(CorrectiveActionAdded ev)
    {
        EnsureScope(ev.TenantId, ev.ProgramId, ev.FindingId);
        EnsureRecorded();
        EnsureNextRevision(ev.Revision);
        if (_actions.Count >= 50 || _actions.Exists(action => action.ActionId == ev.Action.ActionId))
            throw new InvalidOperationException("The finding history exceeds its action bound.");
        _revision = ev.Revision;
        _actions.Add(ev.Action with
        {
            Evidence = ev.Action.Evidence is null ? null : Array.AsReadOnly(ev.Action.Evidence.ToArray()),
        });
    }

    void Apply(CorrectiveActionCompleted ev)
    {
        EnsureScope(ev.TenantId, ev.ProgramId, ev.FindingId);
        EnsureRecorded();
        EnsureNextRevision(ev.Revision);
        var index = _actions.FindIndex(action => action.ActionId == ev.ActionId);
        if (index < 0 || _actions[index].Status == Completed)
            throw new InvalidOperationException(
                "The finding event completes an unknown or already completed action.");
        _revision = ev.Revision;
        _actions[index] = _actions[index] with
        {
            Status = Completed,
            ResolutionNotes = ev.ResolutionNotes,
            Evidence = Array.AsReadOnly(ev.Evidence.ToArray()),
            CompletedByMemberId = ev.CompletedByMemberId,
            CompletedBy = ev.CompletedBy,
            CompletedAt = ev.CompletedAt,
        };
    }

    void Apply(FindingAcceptanceLinked ev)
    {
        EnsureScope(ev.TenantId, ev.ProgramId, ev.FindingId);
        EnsureRecorded();
        EnsureNextRevision(ev.Revision);
        _revision = ev.Revision;
    }

    void Apply(FindingClosed ev)
    {
        EnsureScope(ev.TenantId, ev.ProgramId, ev.FindingId);
        EnsureRecorded();
        EnsureNextRevision(ev.Revision);
        if (_closure is not null || _actions.Count == 0 ||
            _actions.Exists(static action => action.Status != Completed))
            throw new InvalidOperationException(
                "A finding can close only once after all corrective actions are complete.");
        _revision = ev.Revision;
        _closure = ev.Closure with { ResolutionEvidence = Array.AsReadOnly(ev.Closure.ResolutionEvidence.ToArray()) };
    }

    void Apply(FindingReopened ev)
    {
        EnsureScope(ev.TenantId, ev.ProgramId, ev.FindingId);
        EnsureRecorded();
        EnsureNextRevision(ev.Revision);
        if (_closure is null)
            throw new InvalidOperationException("Only a closed finding can be reopened.");
        _revision = ev.Revision;
        _closure = null;
    }

    void ApplyImportedMarker(FindingHistoryEventImported ev)
    {
        EnsureScope(ev.TenantId, ev.ProgramId, ev.FindingId);
        if (_pendingImport is not null || ev.Revision != _revision + 1 ||
            ev.SourceEventId == Uuid.Empty || ev.SourceProgramStreamOffset == ulong.MaxValue ||
            ev.SourceProgramStreamVersion == 0 ||
            ev.SourceOccurredAt == default || ev.SourceOccurredAt.Offset != TimeSpan.Zero ||
            !FindingHistoryEventPayloads.IsHash(
                ev.SourceEventSha256) || _lastImported is { } prior &&
            (ev.SourceProgramStreamOffset <= prior.SourceProgramStreamOffset ||
             ev.SourceProgramStreamVersion <= prior.SourceProgramStreamVersion))
            throw new InvalidOperationException("The imported finding lineage is invalid or out of order.");
        _pendingImport = ev;
    }

    void EnsureScope(Uuid tenantId, Uuid programId, Uuid findingId)
    {
        if (tenantId != _tenantId || programId != _programId || findingId != _findingId)
            throw new InvalidOperationException("The finding event belongs to another stream scope.");
    }

    void EnsureNextRevision(long revision)
    {
        if (revision != _revision + 1)
            throw new InvalidOperationException("The finding event revision is not contiguous.");
    }

    void EnsureRecorded()
    {
        if (_raised is null)
            throw new InvalidOperationException("A finding event precedes its raise event.");
    }

    CommandFailure? Writable(long expectedRevision)
    {
        EnsureImportComplete();
        if (!IsRecorded)
            return CommandFailure.MissingRecord("The finding was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("finding",
                _revision));
        return _closure is not null
            ? CommandFailure.StateConflict("The finding is closed. Reopen it to change it.")
            : null;
    }

    static CommandFailure? ValidateTriage(string severity, Uuid ownerMemberId, DateOnly dueOn) =>
        !Severities.Contains(severity) || ownerMemberId == Uuid.Empty || dueOn == default
            ? CommandFailure.InvalidContent(
                "A finding needs a severity of low, medium, high, or critical, an owner, and a due date.")
            : null;

    static string? EvidenceProblem(IReadOnlyList<EvidenceReference> evidence) =>
        evidence.Count is < 1 or > 50 || evidence.Any(static item => item is null ||
            item.Kind is not ("artifact" or "record" or "external") ||
            string.IsNullOrWhiteSpace(item.Reference) || item.Reference.Length > 2000 ||
            item.Description is { Length: > 2000 })
            ? "invalid"
            : null;

    static bool FitsEventPayload<T>(T ev, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.SerializeToUtf8Bytes(ev, typeInfo).Length <= MaximumEventPayloadBytes;

    static bool SameImport(FindingHistoryEventImported left, FindingHistoryEventImported right) =>
        left.TenantId == right.TenantId && left.ProgramId == right.ProgramId &&
        left.FindingId == right.FindingId && left.Revision == right.Revision &&
        left.SourceEventId == right.SourceEventId &&
        left.SourceProgramStreamOffset == right.SourceProgramStreamOffset &&
        left.SourceProgramStreamVersion == right.SourceProgramStreamVersion &&
        left.SourceEventKind == right.SourceEventKind &&
        left.SourceEventSha256 == right.SourceEventSha256 &&
        left.SourceOccurredAt == right.SourceOccurredAt;

    static CommandFailure EventTooLarge() => CommandFailure.InvalidContent(
        "The finding event exceeds the supported serialized payload size.");

    static EvidenceReference[] AsUnresolved(IReadOnlyList<EvidenceReference> evidence) =>
        evidence.Select(static item => new EvidenceReference(item.ExpectedEvidenceIndex, item.Kind,
            item.Reference.Trim(), string.IsNullOrWhiteSpace(item.Description)
                ? null
                : item.Description.Trim(), "unresolved")).ToArray();

    static bool IsBoundedText(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximum;
}

public readonly record struct FindingAcceptanceIdentity(string Kind, Uuid RecordId,
    Uuid? DecisionId);
