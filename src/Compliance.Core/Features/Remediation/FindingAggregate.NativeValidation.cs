using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

public sealed partial class FindingAggregate
{
    static bool ValidNative(DomainEvent ev)
    {
        if (FindingEventFacts.RequestId(ev) is not { } request || request == Uuid.Empty)
            return false;
        var valid = ev switch
        {
            FindingRaisedV2 x => ValidSource(x.Source) && IsBoundedText(x.Title, 200) &&
                IsBoundedText(x.Description, 8000) && IsBoundedText(x.AffectedScope, 2000) &&
                ValidateTriage(x.Severity, x.OwnerMemberId, x.DueOn) is null &&
                x.Links is { Count: <= 50 } && x.Links.All(static link => link is not null &&
                    LinkKinds.Contains(link.Kind) && IsBoundedText(link.Reference, 500)) &&
                x.CreationKind is "manual" or "routed" &&
                (x.InitialActionSha256 is null || x.CreationKind == "routed" && FindingHistoryEventPayloads.IsHash(x.InitialActionSha256)) &&
                ValidActorAt(x.RaisedBy, x.RaisedAt),
            FindingRevisedV2 x => ValidateTriage(x.Severity, x.OwnerMemberId, x.DueOn) is null &&
                IsBoundedText(x.AffectedScope, 2000) && (x.RootCause is null || IsBoundedText(x.RootCause, MaximumTextLength)) &&
                IsBoundedText(x.Reason, MaximumTextLength) && ValidActorAt(x.RevisedBy, x.RevisedAt),
            CorrectiveActionAddedV2 x => x.Action is { } action && action.ActionId != Uuid.Empty &&
                IsBoundedText(action.Description, MaximumTextLength) && action.OwnerMemberId != Uuid.Empty &&
                action.DueOn != default && action.Status == Open && action.Evidence is null &&
                action.ResolutionNotes is null && action.CompletedAt is null && action.CompletedBy is null &&
                action.CompletedByMemberId is null && ValidActorAt(action.AddedBy, action.AddedAt),
            CorrectiveActionCompletedV2 x => x.ActionId != Uuid.Empty && x.CompletedByMemberId != Uuid.Empty &&
                IsBoundedText(x.ResolutionNotes, MaximumTextLength) && x.Evidence is not null &&
                EvidenceProblem(x.Evidence) is null && x.Evidence.All(static item => item.Resolution == Unresolved) &&
                ValidActorAt(x.CompletedBy, x.CompletedAt) && MatchesMember(x.CompletedBy, x.CompletedByMemberId),
            FindingAcceptanceLinkedV2 x => x.Acceptance is { } acceptance && acceptance.RecordId != Uuid.Empty &&
                (acceptance.Kind == Waiver && acceptance.DecisionId is null ||
                 acceptance.Kind == RiskAcceptance && acceptance.DecisionId is { } decision && decision != Uuid.Empty) &&
                acceptance.State == "active" && acceptance.ExpiresAt.Offset == TimeSpan.Zero &&
                acceptance.ExpiresAt > acceptance.LinkedAt && ValidActorAt(acceptance.LinkedBy, acceptance.LinkedAt),
            FindingClosedV2 x => x.Closure is { } closure && closure.DecisionId != Uuid.Empty &&
                closure.CloserMemberId != Uuid.Empty && IsBoundedText(closure.VerificationRationale, MaximumTextLength) &&
                IsBoundedText(closure.Rationale, MaximumTextLength) && closure.ResolutionEvidence is not null &&
                EvidenceProblem(closure.ResolutionEvidence) is null && closure.ResolutionEvidence.All(static item => item.Resolution == Unresolved) &&
                closure.SeparationOfDutiesWaiverId != Uuid.Empty && ValidActorAt(closure.ClosedBy, closure.ClosedAt) &&
                MatchesMember(closure.ClosedBy, closure.CloserMemberId),
            FindingReopenedV2 x => IsBoundedText(x.Reason, MaximumTextLength) && ValidActorAt(x.ReopenedBy, x.ReopenedAt),
            _ => false,
        };
        return valid && System.Text.Encoding.UTF8.GetByteCount(FindingEventFacts.Serialize(ev).GetRawText()) <= MaximumEventPayloadBytes;
    }

    static bool ValidSource(FindingSource? source) => source is not null && SourceKinds.Contains(source.Kind) &&
        !string.IsNullOrWhiteSpace(source.SourceText) && source.SourceText.Length <= 8000 &&
        source.Version is not { Length: > 200 } && source.RecordId != Uuid.Empty &&
        (source.Kind is "manual" or "incident" or "consultant_observation" || source.RecordId is not null);

    static bool ValidActorAt(ActorReference? actor, DateTimeOffset at) => actor is
    { Kind: "member" or "system_process" or "platform_operator" or "firm_staff", Id.Length: > 0, Display.Length: > 0 } &&
        !string.IsNullOrWhiteSpace(actor.Id) && !string.IsNullOrWhiteSpace(actor.Display) &&
        (actor.Kind == "system_process" || Uuid.TryParse(actor.Id, null, out var id) && id != Uuid.Empty) &&
        at != default && at.Offset == TimeSpan.Zero;

    static bool MatchesMember(ActorReference actor, Uuid memberId) => actor.Kind != "member" ||
        Uuid.TryParse(actor.Id, null, out var id) && id == memberId;

    CommandFailure? EmitNative(DomainEvent ev)
    {
        if (!ValidNative(ev))
            return CommandFailure.InvalidContent("The native finding command content or attribution is invalid or exceeds its payload limit.");
        RaiseEvent(ev);
        return null;
    }
}
