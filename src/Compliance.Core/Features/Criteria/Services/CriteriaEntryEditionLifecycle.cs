using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Bounded edition lifecycle fold for entry decisions, not a child roster.</summary>
/// <remarks>Entry edits may continue through rename, publication attempts and abort cleanup.</remarks>
sealed class CriteriaEntryEditionLifecycle(Uuid editionId, Uuid draftGeneration)
{
    string _status = "empty";
    Uuid _pending;
    Uuid _attempt;
    long _ordinal;
    string? _label;
    string? _requestedLabel;
    long _labelGeneration;
    Uuid _labelConfirmation;
    string? _witness;
    Uuid _witnessIncarnation;
    ActorAttribution? _publicationActor;
    string? _seedVersion;
    string? _seedFingerprint;
    long _executionFloor = 1;

    public bool Active => _status is "draft" or "renaming" or "confirming" or "publishing" or "abort_cleanup";
    public bool ValueActive => _status == "draft";

    public bool Apply(DomainEvent value)
    {
        switch (value)
        {
            case CriteriaEditionOperationAttemptFenced fence when fence.EditionId == editionId && fence.OperationId != Uuid.Empty &&
                fence.ExecutionGeneration >= _executionFloor && fence.IntentSha256 is { Length: 64 } &&
                fence.IntentSha256.All(static value => value is >= '0' and <= '9' or >= 'a' and <= 'f') &&
                fence.Metadata.AggregateVersion > 0 && fence.ExpectedPosition == fence.Metadata.AggregateVersion - 1:
                _executionFloor = checked(fence.ExecutionGeneration + 1);
                return true;
            case CriteriaEditionDraftRequested request when _status == "empty" && request.EditionId == editionId &&
                request.DraftGeneration == draftGeneration && request.OperationId == draftGeneration &&
                request.Framework == "tsc" && EditionLabelClaim.TryNormalize(request.Label, out var initialLabel) &&
                initialLabel == request.Label && ValidDraftRequest(request):
                _pending = request.OperationId;
                _requestedLabel = request.Label;
                _seedVersion = request.SeedVersion;
                _seedFingerprint = request.SeedFingerprint;
                _status = "pending";
                return true;
            case CriteriaInitialLabelRefused initialRefusal when _status == "pending" && initialRefusal.EditionId == editionId &&
                initialRefusal.OperationId == _pending && initialRefusal.Label == _requestedLabel &&
                initialRefusal.RefusalEventId != Uuid.Empty:
                _status = "needs_label";
                return true;
            case CriteriaInitialLabelRequested requested when _status == "needs_label" && requested.EditionId == editionId &&
                requested.OperationId != Uuid.Empty && EditionLabelClaim.TryNormalize(requested.Label, out var requestedLabel) &&
                requestedLabel == requested.Label && ValidHuman(requested.OperationId, requested.ExecutionGeneration,
                    requested.Actor, requested.Admission, new ChooseInitialEditionLabel(editionId, requested.OperationId, requested.Label)):
                _pending = requested.OperationId;
                _requestedLabel = requested.Label;
                _status = "pending";
                return true;
            case CriteriaEditionDraftActivated activated when _status == "pending" && activated.EditionId == editionId &&
                activated.DraftGeneration == draftGeneration && activated.OperationId == _pending && activated.Framework == "tsc" &&
                activated.Label == _requestedLabel && activated.LabelGeneration > 0 && activated.ReservationEventId != Uuid.Empty:
                _label = activated.Label;
                _labelGeneration = activated.LabelGeneration;
                _status = "activating";
                return true;
            case CriteriaEditionDrafted drafted when _status == "activating" && drafted.EditionId == editionId &&
                drafted.DraftGeneration == draftGeneration && drafted.OperationId == _pending && drafted.Framework == "tsc" &&
                drafted.Label == _label && drafted.LabelGeneration == _labelGeneration && drafted.ConfirmationEventId != Uuid.Empty:
                _labelConfirmation = drafted.ConfirmationEventId;
                _pending = Uuid.Empty;
                _requestedLabel = null;
                _status = "draft";
                return true;
            case CriteriaEditionDraftRequestCancelled draftCancelled when (_status is "pending" or "needs_label" or "activating") &&
                draftCancelled.EditionId == editionId && draftCancelled.TargetOperationId == _pending &&
                draftCancelled.OperationId != draftCancelled.TargetOperationId &&
                ValidHuman(draftCancelled.OperationId, draftCancelled.ExecutionGeneration, draftCancelled.Actor, draftCancelled.Admission,
                    new CancelCriteriaEditionDraftRequest(editionId, draftCancelled.OperationId, draftCancelled.TargetOperationId)):
                _status = "abandoned";
                return true;
            case CriteriaEditionRenameRequested rename when _status == "draft" && rename.EditionId == editionId &&
                rename.OperationId != Uuid.Empty && rename.OldLabel == _label && rename.OldLabelGeneration == _labelGeneration &&
                EditionLabelClaim.TryNormalize(rename.NewLabel, out var nextLabel) && nextLabel == rename.NewLabel && rename.NewLabel != _label &&
                ValidHuman(rename.OperationId, rename.ExecutionGeneration, rename.Actor, rename.Admission,
                    new BeginCriteriaEditionRename(editionId, rename.OperationId, rename.NewLabel)):
                _pending = rename.OperationId;
                _requestedLabel = rename.NewLabel;
                _status = "renaming";
                return true;
            case CriteriaEditionDraftRenamed renamed when _status == "renaming" && renamed.EditionId == editionId &&
                renamed.OperationId == _pending && renamed.OldLabel == _label && renamed.OldLabelGeneration == _labelGeneration &&
                renamed.NewLabel == _requestedLabel && renamed.NewLabelGeneration > 0 && renamed.ReservationEventId != Uuid.Empty:
                _label = renamed.NewLabel;
                _labelGeneration = renamed.NewLabelGeneration;
                _status = "confirming";
                return true;
            case CriteriaEditionRenameCompleted completed when _status == "confirming" && completed.EditionId == editionId &&
                completed.OperationId == _pending && completed.Label == _label && completed.LabelGeneration == _labelGeneration &&
                completed.ConfirmationEventId != Uuid.Empty:
                _labelConfirmation = completed.ConfirmationEventId;
                _pending = Uuid.Empty;
                _requestedLabel = null;
                _status = "draft";
                return true;
            case CriteriaEditionRenameRefused renameRefusal when _status == "renaming" && renameRefusal.EditionId == editionId &&
                renameRefusal.OperationId == _pending:
                _pending = Uuid.Empty;
                _status = "draft";
                return true;
            case CriteriaEditionRenameCancelled renameCancelled when _status == "renaming" && renameCancelled.EditionId == editionId &&
                renameCancelled.RenameOperationId == _pending && renameCancelled.OperationId != renameCancelled.RenameOperationId &&
                ValidHuman(renameCancelled.OperationId, renameCancelled.ExecutionGeneration, renameCancelled.Actor, renameCancelled.Admission,
                    new CancelCriteriaEditionRename(editionId, renameCancelled.OperationId, renameCancelled.RenameOperationId)):
                _pending = Uuid.Empty;
                _status = "draft";
                return true;
            case CriteriaPublicationStarted started when _status == "draft" && started.EditionId == editionId &&
                started.DraftGeneration == draftGeneration && started.PublicationAttemptId != Uuid.Empty &&
                started.AttemptOrdinal == checked(_ordinal + 1) && !string.IsNullOrWhiteSpace(started.WitnessIdentifier) &&
                started.WitnessIncarnationId != Uuid.Empty && started.Label == _label && started.LabelGeneration == _labelGeneration &&
                started.LabelConfirmationEventId == _labelConfirmation && _labelConfirmation != Uuid.Empty && ValidPublicationStart(started):
                _attempt = started.PublicationAttemptId;
                _ordinal = started.AttemptOrdinal;
                _witness = started.WitnessIdentifier;
                _witnessIncarnation = started.WitnessIncarnationId;
                _publicationActor = started.Actor;
                _status = "publishing";
                return true;
            case CriteriaPublicationAborted aborted when _status == "publishing" && aborted.EditionId == editionId &&
                aborted.DraftGeneration == draftGeneration && aborted.PublicationAttemptId == _attempt && aborted.AttemptOrdinal == _ordinal &&
                aborted.WitnessIdentifier == _witness && aborted.WitnessIncarnationId == _witnessIncarnation:
                _status = "abort_cleanup";
                return true;
            case CriteriaPublicationAbortCleanupCompleted cleanup when _status == "abort_cleanup" && cleanup.EditionId == editionId &&
                cleanup.DraftGeneration == draftGeneration && cleanup.PublicationAttemptId == _attempt && cleanup.AttemptOrdinal == _ordinal &&
                cleanup.WitnessIdentifier == _witness && cleanup.WitnessIncarnationId == _witnessIncarnation && cleanup.WitnessFenceEventId != Uuid.Empty:
                _attempt = Uuid.Empty;
                _status = "draft";
                return true;
            case CriteriaEditionPublished published when _status == "publishing" && published.EditionId == editionId &&
                published.DraftGeneration == draftGeneration && published.PublicationAttemptId == _attempt && published.AttemptOrdinal == _ordinal &&
                published.WitnessIdentifier == _witness && published.WitnessIncarnationId == _witnessIncarnation && published.Label == _label &&
                published.LabelGeneration == _labelGeneration && published.LabelConfirmationEventId == _labelConfirmation &&
                published.WitnessPinEventId != Uuid.Empty && published.PublishedBy == _publicationActor:
                _status = "published";
                return true;
            case CriteriaEditionDraftAbandoned abandoned when _status == "draft" && abandoned.EditionId == editionId &&
                abandoned.DraftGeneration == draftGeneration && abandoned.Label == _label && abandoned.LabelGeneration == _labelGeneration &&
                abandoned.ExecutionGeneration >= _executionFloor && CriteriaEntryDecisionSourceRules.ValidAbandonment(abandoned):
                _status = "abandoned";
                return true;
            default:
                return false;
        }
    }

    bool ValidDraftRequest(CriteriaEditionDraftRequested request)
    {
        if (request.OperationId == Uuid.Empty || request.ExecutionGeneration < _executionFloor)
            return false;
        if (request.Admission is null)
        {
            var seed = PlatformCriteriaCatalog.SeedEdition();
            return request.Actor == new ActorAttribution("criteria-seed-bootstrap", "system") &&
                   request.EditionId == seed.EditionId && request.Label == seed.EditionLabel &&
                   request.OperationId == PlatformCriteriaSeedDefinition.OperationOf(seed.EditionId, "edition") &&
                   PlatformCriteriaSeedDefinition.MatchesBinding(request.EditionId, request.SeedVersion, request.SeedFingerprint);
        }
        return request.SeedVersion is null && request.SeedFingerprint is null &&
               ValidHuman(request.OperationId, request.ExecutionGeneration, request.Actor, request.Admission,
                   new DraftCriteriaEdition(editionId, request.OperationId, request.Framework, request.Label));
    }

    bool ValidPublicationStart(CriteriaPublicationStarted started)
    {
        if (started.ExecutionGeneration < _executionFloor)
            return false;
        return started.Admission is null
            ? started.Actor == new ActorAttribution("criteria-seed-bootstrap", "system") &&
              PlatformCriteriaSeedDefinition.MatchesBinding(editionId, _seedVersion, _seedFingerprint)
            : ValidHuman(started.PublicationAttemptId, started.ExecutionGeneration, started.Actor, started.Admission,
                new BeginCriteriaPublication(editionId, started.PublicationAttemptId,
                    started.WitnessIdentifier, started.WitnessIncarnationId));
    }

    bool ValidHuman(Uuid operation, long generation, ActorAttribution actor, AccessAdmissionEvidence? admission, object request)
    {
        if (operation == Uuid.Empty || generation < _executionFloor || admission is null ||
            !CriteriaContentBounds.AdmissionFields(actor, admission))
            return false;
        var digest = request switch
        {
            DraftCriteriaEdition value => AuthoredIntentDigest.Compute(value),
            ChooseInitialEditionLabel value => AuthoredIntentDigest.Compute(value),
            CancelCriteriaEditionDraftRequest value => AuthoredIntentDigest.Compute(value),
            BeginCriteriaEditionRename value => AuthoredIntentDigest.Compute(value),
            CancelCriteriaEditionRename value => AuthoredIntentDigest.Compute(value),
            BeginCriteriaPublication value => AuthoredIntentDigest.Compute(value),
            _ => throw new InvalidOperationException("Unsupported edition lifecycle intent."),
        };
        return AccessAdmissionEvidenceBinding.BindsCriteriaOperator(admission, admission.UserId, actor, operation,
            new CriteriaEdition(editionId).Stream.ToString(), CriteriaEdition.BindAdmissionDigest(digest, generation));
    }
}
