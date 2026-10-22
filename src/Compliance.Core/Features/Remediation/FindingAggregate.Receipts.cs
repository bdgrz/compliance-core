using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

public sealed partial class FindingAggregate
{
    readonly Uuid? _selectedRequestId;
    DomainEvent? _selectedReceipt;
    CorrectiveActionAddedV2? _selectedInitialAction;
    FindingRaisedV2? _pendingCreation;

    public bool HasSelectedReceipt => _selectedReceipt is not null;

    CommandFailure? CheckRequest(Uuid requestId)
    {
        EnsureImportComplete();
        return requestId == Uuid.Empty || _selectedRequestId != requestId
            ? CommandFailure.InvalidContent("A finding mutation requires its selected logical request identity.")
            : null;
    }

    static CommandFailure ChangedRequest() => CommandFailure.StateConflict(
        "The finding request identity was already used for different command intent or attribution.");

    static bool SameActor(ActorReference? left, ActorReference? right) => left is not null && right is not null &&
        left.Kind == right.Kind && left.Id == right.Id;

    CommandFailure? RetryRaise(FindingSource source, string title, string description, string severity,
        string scope, Uuid owner, DateOnly due, IReadOnlyList<FindingLink> links, ActorReference actor,
        string creationKind, CorrectiveActionAddedV2? initialAction) =>
        _selectedReceipt is FindingRaisedV2 ev && ev.CreationKind == creationKind &&
        ev.Source == source && ev.Title == title?.Trim() && ev.Description == description?.Trim() &&
        ev.Severity == severity && ev.AffectedScope == scope?.Trim() && ev.OwnerMemberId == owner &&
        ev.DueOn == due && SameActor(ev.RaisedBy, actor) && ValidLinks(links) &&
        ev.Links.SequenceEqual(links.Select(static link => link with { Reference = link.Reference.Trim() })) &&
        SameInitialAction(_selectedInitialAction, initialAction)
            ? null : ChangedRequest();

    static bool SameInitialAction(CorrectiveActionAddedV2? left, CorrectiveActionAddedV2? right) =>
        left is null ? right is null : right is not null &&
        left.Action.ActionId == right.Action.ActionId && left.Action.Description == right.Action.Description &&
        left.Action.OwnerMemberId == right.Action.OwnerMemberId && left.Action.DueOn == right.Action.DueOn &&
        SameActor(left.Action.AddedBy, right.Action.AddedBy);

    bool TryRetry(Uuid requestId, long expectedRevision, out CommandFailure? failure, string severity,
        Uuid owner, DateOnly due, string scope, string? cause, string text, ActorReference actor)
    {
        _ = requestId;
        failure = _selectedReceipt is FindingRevisedV2 ev && ev.Revision - 1 == expectedRevision &&
            ev.Severity == severity && ev.OwnerMemberId == owner && ev.DueOn == due &&
            ev.AffectedScope == scope?.Trim() && ev.RootCause == Clean(cause) && ev.Reason == text?.Trim() &&
            SameActor(ev.RevisedBy, actor) ? null : ChangedRequest();
        return _selectedReceipt is not null;
    }

    CommandFailure? RetryAction(long revision, Uuid actionId, string description, Uuid owner,
        DateOnly due, ActorReference actor) => _selectedReceipt is CorrectiveActionAddedV2 ev &&
        ev.Revision - 1 == revision && ev.Action.ActionId == actionId && ev.Action.Description == description?.Trim() &&
        ev.Action.OwnerMemberId == owner && ev.Action.DueOn == due && SameActor(ev.Action.AddedBy, actor)
            ? null : ChangedRequest();

    CommandFailure? RetryCompletion(long revision, Uuid actionId, string notes,
        IReadOnlyList<EvidenceReference> evidence, Uuid member, ActorReference actor) =>
        _selectedReceipt is CorrectiveActionCompletedV2 ev && ev.Revision - 1 == revision &&
        ev.ActionId == actionId && ev.ResolutionNotes == notes?.Trim() &&
        ev.CompletedByMemberId == member && SameActor(ev.CompletedBy, actor) &&
        EvidenceProblem(evidence) is null && ev.Evidence.SequenceEqual(AsUnresolved(evidence))
            ? null : ChangedRequest();

    CommandFailure? RetryAcceptance(long revision, string kind, Uuid recordId, Uuid? decisionId,
        ActorReference actor) => _selectedReceipt is FindingAcceptanceLinkedV2 ev &&
        ev.Revision - 1 == revision && ev.Acceptance.Kind == kind && ev.Acceptance.RecordId == recordId &&
        ev.Acceptance.DecisionId == decisionId && SameActor(ev.Acceptance.LinkedBy, actor)
            ? null : ChangedRequest();

    CommandFailure? RetryClosure(long revision, Uuid decisionId, string verification,
        IReadOnlyList<EvidenceReference> evidence, string rationale, Uuid member,
        ActorReference actor, Uuid? waiverId) => _selectedReceipt is FindingClosedV2 ev &&
        ev.Revision - 1 == revision && ev.Closure.DecisionId == decisionId &&
        ev.Closure.VerificationRationale == verification?.Trim() && ev.Closure.Rationale == rationale?.Trim() &&
        ev.Closure.CloserMemberId == member && SameActor(ev.Closure.ClosedBy, actor) &&
        ev.Closure.SeparationOfDutiesWaiverId == waiverId && EvidenceProblem(evidence) is null &&
        ev.Closure.ResolutionEvidence.SequenceEqual(AsUnresolved(evidence)) ? null : ChangedRequest();

    static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static bool ValidLinks(IReadOnlyList<FindingLink> links) => links.Count <= 50 && links.All(static link =>
        link is not null && LinkKinds.Contains(link.Kind) && IsBoundedText(link.Reference, 500));

    internal CommandFailure? RaiseRouted(Uuid requestId, FindingSource source, string title,
        string scope, Uuid owner, DateOnly due, IReadOnlyList<FindingLink> links,
        string? correctiveAction, ActorReference actor, DateTimeOffset at)
    {
        if (CheckRequest(requestId) is { } blocked)
            return blocked;
        if (source is null)
            return CommandFailure.InvalidContent("A routed finding needs its source.");
        if (_selectedReceipt is FindingRaisedV2 previous)
            due = previous.DueOn;
        if (correctiveAction is not null && !IsBoundedText(correctiveAction, MaximumTextLength))
            return CommandFailure.InvalidContent("An initial corrective action needs bounded description.");
        var initial = correctiveAction is null ? null : new CorrectiveActionAddedV2(requestId,
            _tenantId, _programId, _findingId, 2, new CorrectiveActionView(
                Uuid.CreateVersion5(_findingId, "corrective-action"), correctiveAction.Trim(), owner,
                due, Open, actor, at));
        if (_selectedReceipt is not null)
            return RetryRaise(source, title, source.SourceText, RoutedFinding.DefaultSeverity, scope,
                owner, due, links, actor, "routed", initial);
        if (IsRecorded)
            return _raised!.Source.Kind == source.Kind && _raised.Source.RecordId == source.RecordId
                ? null : ChangedRequest();
        if (initial is not null && !ValidNative(initial))
            return EventTooLarge();
        var failure = RaiseCore(requestId, source, title, source.SourceText, RoutedFinding.DefaultSeverity,
            scope, owner, due, links, actor, at, "routed", initial);
        if (failure is null && initial is not null)
            RaiseEvent(initial);
        return failure;
    }

    void ApplyNative(DomainEvent ev)
    {
        var requestId = FindingEventFacts.RequestId(ev);
        if (!ValidNative(ev))
            throw new InvalidOperationException("A native finding event requires a bounded request receipt.");
        var business = FindingEventFacts.Business(ev);
        var (actor, at) = business switch
        {
            FindingRaised x => (x.RaisedBy, x.RaisedAt),
            FindingRevised x => (x.RevisedBy, x.RevisedAt),
            CorrectiveActionAdded x => (x.Action.AddedBy, x.Action.AddedAt),
            CorrectiveActionCompleted x => (x.CompletedBy, x.CompletedAt),
            FindingAcceptanceLinked x => (x.Acceptance.LinkedBy, x.Acceptance.LinkedAt),
            FindingClosed x => (x.Closure.ClosedBy, x.Closure.ClosedAt),
            FindingReopened x => (x.ReopenedBy, x.ReopenedAt),
            _ => throw new InvalidOperationException("Unknown native finding fact."),
        };
        if (actor is not { Kind: "member" or "system_process" or "platform_operator" or "firm_staff", Id.Length: > 0, Display.Length: > 0 } ||
            at == default || at.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("The native finding attribution is invalid.");
        var isInitial = false;
        if (_pendingCreation is { } pending)
        {
            if (!FindingEventFacts.IsInitialActionFor(pending, ev))
                throw new InvalidOperationException("The routed finding creation pair is invalid.");
            _pendingCreation = null;
            isInitial = true;
        }
        if (ev is FindingRaisedV2 raised)
        {
            if (raised.CreationKind is not ("manual" or "routed") ||
                raised.InitialActionSha256 is not null && (raised.CreationKind != "routed" ||
                    !FindingHistoryEventPayloads.IsHash(raised.InitialActionSha256)))
                throw new InvalidOperationException("The finding creation declaration is invalid.");
            if (raised.InitialActionSha256 is not null)
                _pendingCreation = raised;
        }
        if (requestId == _selectedRequestId)
        {
            if (_selectedReceipt is not null && !(isInitial && _selectedReceipt is FindingRaisedV2 && _selectedInitialAction is null))
                throw new InvalidOperationException("A finding request identity appears more than once.");
            if (isInitial)
                _selectedInitialAction = (CorrectiveActionAddedV2)FindingEventFacts.Freeze(ev);
            else
                _selectedReceipt = FindingEventFacts.Freeze(ev);
        }
        base.Apply(business);
    }
}
