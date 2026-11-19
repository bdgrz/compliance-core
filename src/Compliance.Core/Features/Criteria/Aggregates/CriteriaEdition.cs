using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Aggregates;

/// <summary>Owns one edition's label and publication lifecycle, without a collection of criteria.</summary>
/// <remarks>Participant event IDs passed to this owner must first be verified against committed source by the internal caller.</remarks>
public sealed class CriteriaEdition : Aggregate
{
    /// <summary>The platform realm containing criteria editions.</summary>
    public const string Realm = "bdgrz";
    readonly Uuid _editionId;
    readonly Uuid _selectedOperationId;
    // Keys are the fixed participant stages for this selected operation.
    readonly Dictionary<string, ParticipantOutcome> _selectedParticipantFacts = [];
    Operation? _selectedOperation;
    (long Generation, string Intent)? _selectedFence;
    CriteriaPublicationAborted? _selectedAbort;
    string _status = "empty";
    string? _framework;
    string? _label;
    string? _requestedLabel;
    long _labelGeneration;
    Uuid _pendingOperationId;
    bool _pendingParticipantAdmissionCurrent;
    Uuid _currentLabelOperationId;
    Uuid _lastLabelConfirmationEventId;
    long _attemptOrdinal;
    CriteriaPublicationStarted? _attempt;
    CriteriaEditionPublished? _publication;
    string? _seedVersion;
    string? _seedFingerprint;
    Uuid _draftGeneration;
    long _executionFloor = 1;
    Uuid _initialFenceOperationId;
    string? _initialFenceIntent;

    /// <summary>Creates an owner, selecting the one operation whose retry history is needed.</summary>
    /// <remarks>
    /// Unselected owners are for current-state reads. Each mutation must select its
    /// exact operation before hydration. Portia still loads the full edition stream;
    /// selection bounds retained operation state, not stream lifetime or loading cost.
    /// </remarks>
    public CriteriaEdition(Uuid editionId, Uuid selectedOperationId = default) : base(editionId, AddressOf(editionId))
    {
        _editionId = editionId;
        _selectedOperationId = selectedOperationId;
        On<CriteriaEditionDraftRequested>(ApplyDraftRequested);
        On<CriteriaEditionOperationAttemptFenced>(ApplyFence);
        On<CriteriaInitialLabelRequested>(ApplyInitialLabelRequested);
        On<CriteriaInitialLabelRefused>(ApplyInitialLabelRefused);
        On<CriteriaEditionDraftActivated>(ApplyDraftActivated);
        On<CriteriaEditionDrafted>(ApplyDrafted);
        On<CriteriaEditionDraftRequestCancelled>(ApplyDraftCancelled);
        On<CriteriaEditionRenameRequested>(ApplyRenameRequested);
        On<CriteriaEditionDraftRenamed>(ApplyDraftRenamed);
        On<CriteriaEditionRenameCompleted>(ApplyRenameCompleted);
        On<CriteriaEditionRenameRefused>(ApplyRenameRefused);
        On<CriteriaEditionRenameCancelled>(ApplyRenameCancelled);
        On<CriteriaPublicationStarted>(ApplyPublicationStarted);
        On<CriteriaPublicationAborted>(ApplyPublicationAborted);
        On<CriteriaPublicationAbortCleanupCompleted>(ApplyAbortCleanup);
        On<CriteriaEditionDraftAbandoned>(ApplyAbandoned);
        On<CriteriaEditionPublished>(ApplyPublished);
    }

    /// <summary>Gets the edition's exact resource address.</summary>
    public static EventStreamAddress AddressOf(Uuid editionId) =>
        new(Realm, $"criteria-{editionId}", $"edition-{editionId}");

    /// <summary>Gets the edition's exact resource address.</summary>
    public static EventStreamAddress StreamFor(Uuid editionId) => AddressOf(editionId);

    /// <summary>The edition's per-edition ordered event area.</summary>
    public static string AreaFor(Uuid editionId) => $"criteria-{editionId}";

    /// <summary>Current lifecycle state.</summary>
    public string Status => _status;
    /// <summary>The immutable source-recorded framework, or null before the draft request.</summary>
    public string? Framework => _framework;
    /// <summary>The current source label, which can await confirmation during a rename.</summary>
    public string? Label => _label;
    /// <summary>The current source label generation.</summary>
    public long LabelGeneration => _labelGeneration;
    /// <summary>The label being reserved by the current transition.</summary>
    public string? RequestedLabel => _requestedLabel;
    /// <summary>The current label or publication operation, if any.</summary>
    public Uuid PendingOperationId => _pendingOperationId;
    /// <summary>The operation holding the currently confirmed label.</summary>
    public Uuid CurrentLabelOperationId => _currentLabelOperationId;
    /// <summary>The last committed label confirmation source fact.</summary>
    public Uuid LastLabelConfirmationEventId => _lastLabelConfirmationEventId;
    /// <summary>The immutable generation of this edition draft.</summary>
    public Uuid DraftGeneration => _draftGeneration;
    /// <summary>The currently pending publication attempt, including during abort cleanup.</summary>
    public CriteriaPublicationStarted? PendingPublication => _attempt;
    /// <summary>The committed publication fact, when published.</summary>
    public CriteriaEditionPublished? Publication => _publication;
    /// <summary>Whether publication was durably recorded.</summary>
    public bool IsPublished => _status == "published";
    /// <summary>The seed release bound to this edition, when bootstrapped.</summary>
    public string? SeedVersion => _seedVersion;
    /// <summary>The immutable compiled seed fingerprint, when bootstrapped.</summary>
    public string? SeedFingerprint => _seedFingerprint;
    /// <summary>Whether this edition is bound to the configured seed release.</summary>
    public bool IsSeedBound => _seedVersion is not null;
    /// <summary>The target-stream execution generation required for a newly authorized operation.</summary>
    public long NextExecutionGeneration => _executionFloor;

    /// <summary>Binds an authored request digest to the target-stream execution generation.</summary>
    public static string BindAdmissionDigest(string authoredIntentSha256, long executionGeneration)
    {
        if (!IsDigest(authoredIntentSha256) || executionGeneration <= 0)
            throw new ArgumentException("A lowercase SHA-256 digest and positive execution generation are required.");
        var value = "bdgrz.criteria.admission.v1\n" + executionGeneration.ToString(CultureInfo.InvariantCulture) +
            "\n" + authoredIntentSha256;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    /// <summary>Returns an operation's durable status, or null when no operation fact has been observed.</summary>
    public CriteriaOperationReceipt? Receipt(Uuid operationId) => Selected(operationId) && _selectedOperation is { } operation
        ? new CriteriaOperationReceipt(_editionId, operationId, operation.Status, operation.Reason)
        : null;

    /// <summary>Starts a client-identified draft using trusted, server-produced admission evidence.</summary>
    public Result Draft(Uuid operationId, string framework, string label, ActorAttribution actor, AccessAdmissionEvidence admission,
        long executionGeneration = 1)
    {
        if (framework != "tsc" || !EditionLabelClaim.TryNormalize(label, out var normalized))
            return Fail(RequestErrorKind.Validation, "The supported tsc framework and a valid structured edition label are required.");
        var request = new DraftCriteriaEdition(_editionId, operationId, framework, normalized);
        var digest = AuthoredIntentDigest.Compute(request);
        var check = Start(operationId, executionGeneration, "draft", digest, actor, admission, out var replay);
        if (!check.IsSuccess || replay)
            return check;
        if (_status != "empty")
            return Conflict("The edition identity is already used.");
        if (_initialFenceOperationId != Uuid.Empty &&
            (_initialFenceOperationId != operationId || _initialFenceIntent != digest))
            return Conflict("The edition identity is bound to a different recovering draft intent.");
        RaiseEvent(new CriteriaEditionDraftRequested(_editionId, operationId, operationId, executionGeneration,
            framework, normalized, actor, admission, null, null));
        return Result.Success;
    }

    /// <summary>Starts the configured seed; callable only by the trusted internal bootstrap.</summary>
    internal Result DraftSeed(Uuid operationId, string label, string seedVersion, string seedFingerprint,
        long executionGeneration = 1)
    {
        var seed = PlatformCriteriaCatalog.SeedEdition();
        if (_editionId != seed.EditionId || seed.Framework != "tsc" || !string.Equals(label, seed.EditionLabel, StringComparison.Ordinal) ||
            !string.Equals(seedVersion, CriteriaSeedFingerprint.Version, StringComparison.Ordinal) ||
            !string.Equals(seedFingerprint, CriteriaSeedFingerprint.Compute(seed, PlatformCriteriaCatalog.EnumerateSeedEntries()),
                StringComparison.Ordinal) || operationId == Uuid.Empty || executionGeneration < _executionFloor)
            return Fail(RequestErrorKind.Validation, "The configured seed identity, version and fingerprint are required.");
        var actor = new ActorAttribution("criteria-seed-bootstrap", "system");
        if (!Selected(operationId))
            return Conflict("The exact operation must be selected before loading the edition.");
        if (_selectedOperation is { } prior)
            return prior.Kind == "seed-draft" && prior.Digest == seedFingerprint && prior.Actor == actor
                ? Result.Success : Conflict("The operation identity has different intent.");
        if (_status != "empty")
            return Conflict("The edition identity is already used.");
        RaiseEvent(new CriteriaEditionDraftRequested(_editionId, operationId, operationId, executionGeneration,
            seed.Framework, label, actor, null, seedVersion, seedFingerprint));
        return Result.Success;
    }

    /// <summary>Records a source-position fence without creating an edition.</summary>
    public Result FenceOperationAttempt(Uuid operationId, long executionGeneration, ulong expectedPosition, string intentSha256)
    {
        if (operationId == Uuid.Empty || executionGeneration <= 0 || !IsDigest(intentSha256))
            return Fail(RequestErrorKind.Validation, "The operation, generation and intent digest are required.");
        if (!Selected(operationId))
            return Conflict("The exact operation must be selected before loading the edition.");
        if (_selectedOperation is not null)
            return Conflict("The operation already committed; reconcile its actual outcome instead of reporting a fence.");
        if (_selectedFence is { } prior)
        {
            if (!string.Equals(prior.Intent, intentSha256, StringComparison.Ordinal))
                return Conflict("The operation identity has different intent.");
            if (prior.Generation >= executionGeneration)
                return Result.Success;
        }
        if (CommittedStreamPosition != expectedPosition)
            return Conflict("The edition source position changed; reconcile the committed winner.");
        if (executionGeneration < _executionFloor)
            return Conflict("This execution generation has already been fenced.");
        if (_status == "empty" && _initialFenceOperationId != Uuid.Empty &&
            (_initialFenceOperationId != operationId || _initialFenceIntent != intentSha256))
            return Conflict("The edition identity is bound to a different recovering draft intent.");
        RaiseEvent(new CriteriaEditionOperationAttemptFenced(_editionId, operationId, executionGeneration,
            expectedPosition, intentSha256));
        return Result.Success;
    }

    /// <summary>Chooses another initial label after an exact terminal refusal.</summary>
    public Result ChooseInitialLabel(Uuid operationId, string label, ActorAttribution actor, AccessAdmissionEvidence admission,
        long executionGeneration = 1)
    {
        if (!EditionLabelClaim.TryNormalize(label, out var normalized))
            return Fail(RequestErrorKind.Validation, "A valid structured edition label is required.");
        var digest = AuthoredIntentDigest.Compute(new ChooseInitialEditionLabel(_editionId, operationId, normalized));
        var check = Start(operationId, executionGeneration, "initial-label", digest, actor, admission, out var replay);
        if (!check.IsSuccess || replay)
            return check;
        if (_status != "needs_label")
            return Conflict("The prior label attempt must be definitively refused.");
        RaiseEvent(new CriteriaInitialLabelRequested(_editionId, operationId, executionGeneration, normalized, actor, admission));
        return Result.Success;
    }

    /// <summary>Records an exact source-verified initial-label refusal.</summary>
    public Result RecordInitialLabelRefusal(Uuid operationId, Uuid refusalFactId, string reason)
    {
        if (TryParticipant(operationId, "initial-refusal", refusalFactId, out var prior, reason: reason))
            return prior;
        if (!_pendingParticipantAdmissionCurrent)
            return Conflict("The legacy operation requires an explicitly qualified recovery policy.");
        if (_status != "pending" || _pendingOperationId != operationId || refusalFactId == Uuid.Empty || string.IsNullOrWhiteSpace(reason))
            return Conflict("The exact pending label and definitive refusal are required.");
        RaiseEvent(new CriteriaInitialLabelRefused(_editionId, operationId, _requestedLabel!, reason, refusalFactId));
        return Result.Success;
    }

    /// <summary>Records the pending activation of an exact committed label reservation.</summary>
    public Result ActivateDraft(Uuid operationId, long labelGeneration, Uuid reservationEventId)
    {
        if (TryParticipant(operationId, "activate", reservationEventId, out var prior, labelGeneration))
            return prior;
        if (!_pendingParticipantAdmissionCurrent)
            return Conflict("The legacy operation requires an explicitly qualified recovery policy.");
        if (_status != "pending" || _pendingOperationId != operationId || labelGeneration <= 0 || reservationEventId == Uuid.Empty)
            return Conflict("The exact pending operation and label reservation are required.");
        RaiseEvent(new CriteriaEditionDraftActivated(_editionId, _draftGeneration, operationId, _framework!, _requestedLabel!, labelGeneration, reservationEventId));
        return Result.Success;
    }

    /// <summary>Completes activation only after the exact claim confirmation.</summary>
    public Result CompleteDraft(Uuid operationId, long labelGeneration, Uuid confirmationEventId)
    {
        if (TryParticipant(operationId, "drafted", confirmationEventId, out var prior, labelGeneration))
            return prior;
        if (!_pendingParticipantAdmissionCurrent)
            return Conflict("The legacy operation requires an explicitly qualified recovery policy.");
        if (_status != "activating" || _pendingOperationId != operationId || _labelGeneration != labelGeneration || confirmationEventId == Uuid.Empty)
            return Conflict("The exact activation and confirmed label are required.");
        RaiseEvent(new CriteriaEditionDrafted(_editionId, _draftGeneration, operationId, _framework!, _label!, labelGeneration, confirmationEventId));
        return Result.Success;
    }

    /// <summary>Cancels the exact initial operation before its completion.</summary>
    public Result CancelDraftRequest(Uuid operationId, Uuid targetOperationId, ActorAttribution actor,
        AccessAdmissionEvidence admission, long executionGeneration = 1)
    {
        var digest = AuthoredIntentDigest.Compute(new CancelCriteriaEditionDraftRequest(_editionId, operationId, targetOperationId));
        var check = Start(operationId, executionGeneration, "cancel-draft", digest, actor, admission, out var replay);
        if (!check.IsSuccess || replay)
            return check;
        if (_status is not ("pending" or "needs_label" or "activating") || _pendingOperationId != targetOperationId)
            return Conflict("The exact initial request must still be unfinished.");
        RaiseEvent(new CriteriaEditionDraftRequestCancelled(_editionId, operationId, executionGeneration, targetOperationId, actor, admission));
        return Result.Success;
    }

    /// <summary>Begins one label change while retaining the original confirmed claim.</summary>
    public Result BeginRename(Uuid operationId, string label, ActorAttribution actor, AccessAdmissionEvidence admission,
        long executionGeneration = 1)
    {
        if (!EditionLabelClaim.TryNormalize(label, out var normalized))
            return Fail(RequestErrorKind.Validation, "A valid structured edition label is required.");
        var digest = AuthoredIntentDigest.Compute(new BeginCriteriaEditionRename(_editionId, operationId, normalized));
        var check = Start(operationId, executionGeneration, "rename", digest, actor, admission, out var replay);
        if (!check.IsSuccess || replay)
            return check;
        if (_status != "draft")
            return Conflict("The edition must be an active draft without another transition.");
        if (string.Equals(_label, normalized, StringComparison.Ordinal))
            return Fail(RequestErrorKind.Validation, "The edition already has this label.");
        RaiseEvent(new CriteriaEditionRenameRequested(_editionId, operationId, executionGeneration, _label!, _labelGeneration,
            normalized, actor, admission));
        return Result.Success;
    }

    /// <summary>Commits the exact rename after the destination claim is reserved.</summary>
    public Result CommitRename(Uuid operationId, long labelGeneration, Uuid reservationEventId)
    {
        if (TryParticipant(operationId, "rename-commit", reservationEventId, out var prior, labelGeneration))
            return prior;
        if (!_pendingParticipantAdmissionCurrent)
            return Conflict("The legacy operation requires an explicitly qualified recovery policy.");
        if (_status != "renaming" || _pendingOperationId != operationId || labelGeneration <= 0 || reservationEventId == Uuid.Empty)
            return Conflict("The exact pending rename and destination reservation are required.");
        RaiseEvent(new CriteriaEditionDraftRenamed(_editionId, operationId, _label!, _labelGeneration, _requestedLabel!, labelGeneration, reservationEventId));
        return Result.Success;
    }

    /// <summary>Completes a rename only after the new claim confirmation.</summary>
    public Result CompleteRename(Uuid operationId, Uuid confirmationEventId)
    {
        if (TryParticipant(operationId, "rename-complete", confirmationEventId, out var prior))
            return prior;
        if (!_pendingParticipantAdmissionCurrent)
            return Conflict("The legacy operation requires an explicitly qualified recovery policy.");
        if (_status != "confirming" || _pendingOperationId != operationId || confirmationEventId == Uuid.Empty)
            return Conflict("The exact renamed edition and destination confirmation are required.");
        RaiseEvent(new CriteriaEditionRenameCompleted(_editionId, operationId, _label!, _labelGeneration, confirmationEventId));
        return Result.Success;
    }

    /// <summary>Records a definitive refusal before the edition label changes.</summary>
    public Result RefuseRename(Uuid operationId, Uuid refusalFactId, string reason)
    {
        if (TryParticipant(operationId, "rename-refusal", refusalFactId, out var prior, reason: reason))
            return prior;
        if (!_pendingParticipantAdmissionCurrent)
            return Conflict("The legacy operation requires an explicitly qualified recovery policy.");
        if (_status != "renaming" || _pendingOperationId != operationId || refusalFactId == Uuid.Empty || string.IsNullOrWhiteSpace(reason))
            return Conflict("The exact pending rename and definitive refusal are required.");
        RaiseEvent(new CriteriaEditionRenameRefused(_editionId, operationId, reason, refusalFactId));
        return Result.Success;
    }

    /// <summary>Cancels a rename only while its edition change has not committed.</summary>
    public Result CancelRename(Uuid operationId, Uuid renameOperationId, ActorAttribution actor,
        AccessAdmissionEvidence admission, long executionGeneration = 1)
    {
        var digest = AuthoredIntentDigest.Compute(new CancelCriteriaEditionRename(_editionId, operationId, renameOperationId));
        var check = Start(operationId, executionGeneration, "cancel-rename", digest, actor, admission, out var replay);
        if (!check.IsSuccess || replay)
            return check;
        if (_status != "renaming" || _pendingOperationId != renameOperationId)
            return Conflict("The exact rename must be pending and unchanged.");
        RaiseEvent(new CriteriaEditionRenameCancelled(_editionId, operationId, executionGeneration, renameOperationId, actor, admission));
        return Result.Success;
    }

    /// <summary>Starts publication for one exact witness without reading or recording other entries.</summary>
    public Result BeginPublication(Uuid attemptId, string witnessIdentifier, Uuid witnessIncarnationId,
        ActorAttribution actor, AccessAdmissionEvidence admission, long executionGeneration = 1)
    {
        var digest = AuthoredIntentDigest.Compute(new BeginCriteriaPublication(_editionId, attemptId, witnessIdentifier, witnessIncarnationId));
        var check = Start(attemptId, executionGeneration, "publish", digest, actor, admission, out var replay);
        if (!check.IsSuccess || replay)
            return check;
        return BeginPublicationCore(attemptId, executionGeneration, witnessIdentifier, witnessIncarnationId, actor, admission);
    }

    /// <summary>Starts publication for the exact configured seed under trusted bootstrap authority.</summary>
    internal Result BeginSeedPublication(Uuid attemptId, string witnessIdentifier, Uuid witnessIncarnationId,
        long executionGeneration = 1)
    {
        if (!IsSeedBound || _editionId != PlatformCriteriaCatalog.SeedEdition().EditionId || executionGeneration < _executionFloor)
            return Conflict("The configured seed binding is required.");
        var actor = new ActorAttribution("criteria-seed-bootstrap", "system");
        if (!Selected(attemptId))
            return Conflict("The exact operation must be selected before loading the edition.");
        if (_selectedOperation is { } prior)
            return prior.Kind == "seed-publish" && prior.Digest == witnessIdentifier + ":" + witnessIncarnationId
                ? Result.Success : Conflict("The operation identity has different intent.");
        return BeginPublicationCore(attemptId, executionGeneration, witnessIdentifier, witnessIncarnationId, actor, null);
    }

    Result BeginPublicationCore(Uuid attemptId, long executionGeneration, string witnessIdentifier,
        Uuid witnessIncarnationId, ActorAttribution actor, AccessAdmissionEvidence? admission)
    {
        if (_status != "draft" || attemptId == Uuid.Empty || string.IsNullOrWhiteSpace(witnessIdentifier) ||
            witnessIncarnationId == Uuid.Empty || _lastLabelConfirmationEventId == Uuid.Empty)
            return Conflict("An active draft, confirmed label and exact criterion witness are required.");
        RaiseEvent(new CriteriaPublicationStarted(_editionId, _draftGeneration, attemptId, executionGeneration,
            checked(_attemptOrdinal + 1), witnessIdentifier, witnessIncarnationId, _label!, _labelGeneration,
            _lastLabelConfirmationEventId, actor, admission));
        return Result.Success;
    }

    /// <summary>Commits the publication fact after source-verifying the exact witness pin and current claim.</summary>
    public Result Publish(Uuid attemptId, Uuid witnessPinEventId)
    {
        if (!Selected(attemptId))
            return Conflict("The exact operation must be selected before loading the edition.");
        if (_publication is { } prior)
            return prior.PublicationAttemptId == attemptId && prior.WitnessPinEventId == witnessPinEventId
                ? Result.Success : Conflict("The edition is already published under a different attempt.");
        if (_status != "publishing" || _attempt is null || _attempt.PublicationAttemptId != attemptId || witnessPinEventId == Uuid.Empty)
            return Conflict("The exact live attempt and source-verified witness pin are required.");
        if (_attempt.Admission is not null && !AccessAdmissionEvidenceBinding.IsCurrentFormat(_attempt.Admission))
            return Conflict("The legacy publication requires an explicitly qualified recovery policy.");
        RaiseEvent(new CriteriaEditionPublished(_editionId, _draftGeneration, attemptId, _attempt.AttemptOrdinal,
            _attempt.WitnessIdentifier, _attempt.WitnessIncarnationId, _label!, _labelGeneration, witnessPinEventId,
            _lastLabelConfirmationEventId, _attempt.Actor));
        return Result.Success;
    }

    /// <summary>Commits the edition-side abort before fencing the witness target.</summary>
    public Result AbortPublication(Uuid attemptId, string reason)
    {
        if (!Selected(attemptId))
            return Conflict("The exact operation must be selected before loading the edition.");
        if (_selectedAbort is { } prior)
            return string.Equals(prior.Reason, reason, StringComparison.Ordinal) ? Result.Success : Conflict("The abort intent changed.");
        if (_status != "publishing" || _attempt is null || _attempt.PublicationAttemptId != attemptId || string.IsNullOrWhiteSpace(reason))
            return Conflict("The exact live publication attempt and reason are required.");
        RaiseEvent(new CriteriaPublicationAborted(_editionId, _draftGeneration, attemptId, _attempt.AttemptOrdinal,
            _attempt.WitnessIdentifier, _attempt.WitnessIncarnationId, reason));
        return Result.Success;
    }

    /// <summary>Reopens the draft only after the exact target-side witness fence was committed.</summary>
    public Result ConfirmPublicationAbortCleanup(Uuid attemptId, Uuid witnessFenceEventId)
    {
        if (TryParticipant(attemptId, "abort-cleanup", witnessFenceEventId, out var prior))
            return prior;
        if (_status != "abort_cleanup" || _attempt is null || _attempt.PublicationAttemptId != attemptId || witnessFenceEventId == Uuid.Empty)
            return Conflict("The exact aborted attempt and committed witness fence are required.");
        RaiseEvent(new CriteriaPublicationAbortCleanupCompleted(_editionId, _draftGeneration, attemptId, _attempt.AttemptOrdinal,
            _attempt.WitnessIdentifier, _attempt.WitnessIncarnationId, witnessFenceEventId));
        return Result.Success;
    }

    /// <summary>Abandons an active draft with no unresolved transition.</summary>
    public Result Abandon(Uuid operationId, ActorAttribution actor, AccessAdmissionEvidence admission, long executionGeneration = 1)
    {
        var digest = AuthoredIntentDigest.Compute(new AbandonCriteriaEditionDraft(_editionId, operationId));
        var check = Start(operationId, executionGeneration, "abandon", digest, actor, admission, out var replay);
        if (!check.IsSuccess || replay)
            return check;
        if (_status != "draft")
            return Conflict("Only an active draft without an unresolved transition can be abandoned.");
        RaiseEvent(new CriteriaEditionDraftAbandoned(_editionId, _draftGeneration, operationId, executionGeneration,
            _label!, _labelGeneration, actor, admission));
        return Result.Success;
    }

    Result Start(Uuid operationId, long generation, string kind, string digest, ActorAttribution actor,
        AccessAdmissionEvidence admission, out bool replay)
    {
        replay = false;
        if (!AccessAdmissionEvidenceBinding.IsCurrentFormat(admission) || !Selected(operationId) || _editionId == Uuid.Empty || operationId == Uuid.Empty || generation < _executionFloor || actor is null || admission is null ||
            actor.Subject != admission.ActorSubject || actor.Issuer != admission.ActorIssuer || admission.UserId == Uuid.Empty ||
            admission.OperationId != operationId || admission.Target != Stream.ToString() || admission.Operator is null ||
            admission.Identity is null || admission.Policy is null ||
            !string.Equals(admission.IntentSha256, BindAdmissionDigest(digest, generation), StringComparison.Ordinal))
            return Fail(RequestErrorKind.Validation, "The exact trusted actor, target, operation and authored intent admission are required.");
        if (_selectedOperation is { } old)
        {
            if (old.Kind != kind || old.Digest != digest || old.Actor != actor)
                return Conflict("The operation identity already has different intent.");
            replay = true;
            return Result.Success;
        }
        if (_selectedFence is { } fence &&
            (!string.Equals(fence.Intent, digest, StringComparison.Ordinal) || generation <= fence.Generation))
            return Conflict("The prior execution is fenced or the operation identity has different intent.");
        return Result.Success;
    }

    bool TryParticipant(Uuid operationId, string stage, Uuid factId, out Result result, long generation = 0,
        string? reason = null)
    {
        if (!Selected(operationId))
        {
            result = Conflict("The exact operation must be selected before loading the edition.");
            return true;
        }
        if (_selectedParticipantFacts.TryGetValue(stage, out var prior))
        {
            result = prior == new ParticipantOutcome(factId, generation, reason) && factId != Uuid.Empty
                ? Result.Success : Conflict("The participant outcome changed.");
            return true;
        }
        result = Result.Success;
        return false;
    }

    static bool IsDigest(string? value) => value is { Length: 64 } &&
        value.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    static Result Fail(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
    static Result Conflict(string message) => Fail(RequestErrorKind.Conflict, message);
    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    void ApplyDraftRequested(CriteriaEditionDraftRequested e)
    {
        Require(e.EditionId == _editionId && _editionId != Uuid.Empty && _status == "empty" && e.OperationId != Uuid.Empty &&
            e.DraftGeneration == e.OperationId && e.ExecutionGeneration >= _executionFloor && e.Framework == "tsc" &&
            EditionLabelClaim.TryNormalize(e.Label, out var normalized) &&
            normalized == e.Label && !IsRecorded(e.OperationId), "Invalid edition draft history.");
        var seed = e.SeedVersion is not null || e.SeedFingerprint is not null;
        if (seed)
        {
            Require(e.Admission is null && e.Actor == new ActorAttribution("criteria-seed-bootstrap", "system") &&
                e.SeedVersion == CriteriaSeedFingerprint.Version && IsDigest(e.SeedFingerprint) &&
                _editionId == PlatformCriteriaCatalog.SeedEdition().EditionId, "Invalid seed binding in edition history.");
            Record(e.OperationId, new("seed-draft", e.SeedFingerprint!, e.Actor, "pending", null));
        }
        else
        {
            var digest = AuthoredIntentDigest.Compute(new DraftCriteriaEdition(_editionId, e.OperationId, e.Framework, e.Label));
            Require(e.Admission is not null && ValidReplayAdmission(e.OperationId, e.ExecutionGeneration, e.Actor, e.Admission, digest), "Invalid draft admission.");
            Require(_initialFenceOperationId == Uuid.Empty ||
                (_initialFenceOperationId == e.OperationId && _initialFenceIntent == digest), "Draft does not match its recovery fence.");
            Record(e.OperationId, new("draft", digest, e.Actor, "pending", null));
        }
        _framework = e.Framework;
        _seedVersion = e.SeedVersion;
        _seedFingerprint = e.SeedFingerprint;
        _draftGeneration = e.DraftGeneration;
        _pendingParticipantAdmissionCurrent = seed || AccessAdmissionEvidenceBinding.IsCurrentFormat(e.Admission);
        _requestedLabel = e.Label;
        _pendingOperationId = e.OperationId;
        _status = "pending";
    }

    void ApplyFence(CriteriaEditionOperationAttemptFenced e)
    {
        Require(e.EditionId == _editionId && e.OperationId != Uuid.Empty && e.ExecutionGeneration >= _executionFloor && IsDigest(e.IntentSha256) &&
            !IsRecorded(e.OperationId) && (!Selected(e.OperationId) || _selectedFence is not { } old ||
            (old.Generation < e.ExecutionGeneration && old.Intent == e.IntentSha256)), "Invalid edition operation fence history.");
        if (Selected(e.OperationId))
            _selectedFence = (e.ExecutionGeneration, e.IntentSha256);
        _executionFloor = checked(e.ExecutionGeneration + 1);
        if (_status == "empty")
        {
            Require(_initialFenceOperationId == Uuid.Empty ||
                (_initialFenceOperationId == e.OperationId && _initialFenceIntent == e.IntentSha256), "Conflicting initial recovery fence.");
            _initialFenceOperationId = e.OperationId;
            _initialFenceIntent = e.IntentSha256;
        }
    }

    void ApplyInitialLabelRequested(CriteriaInitialLabelRequested e)
    {
        var digest = AuthoredIntentDigest.Compute(new ChooseInitialEditionLabel(_editionId, e.OperationId, e.Label));
        Require(e.EditionId == _editionId && _status == "needs_label" && EditionLabelClaim.TryNormalize(e.Label, out var normalized) &&
            normalized == e.Label && ValidReplayAdmission(e.OperationId, e.ExecutionGeneration, e.Actor, e.Admission, digest) &&
            !IsRecorded(e.OperationId), "Invalid initial label selection history.");
        Record(e.OperationId, new("initial-label", digest, e.Actor, "pending", null));
        _pendingParticipantAdmissionCurrent = AccessAdmissionEvidenceBinding.IsCurrentFormat(e.Admission);
        _pendingOperationId = e.OperationId;
        _requestedLabel = e.Label;
        _status = "pending";
    }

    void ApplyInitialLabelRefused(CriteriaInitialLabelRefused e)
    {
        Require(e.EditionId == _editionId && _status == "pending" && _pendingOperationId == e.OperationId &&
            e.Label == _requestedLabel && e.RefusalEventId != Uuid.Empty && !string.IsNullOrWhiteSpace(e.Reason), "Invalid initial label refusal history.");
        RecordParticipant(e.OperationId, "initial-refusal", e.RefusalEventId, reason: e.Reason);
        SetOutcome(e.OperationId, "refused", e.Reason);
        _status = "needs_label";
    }

    void ApplyDraftActivated(CriteriaEditionDraftActivated e)
    {
        Require(e.EditionId == _editionId && e.DraftGeneration == _draftGeneration && e.Framework == _framework && _framework == "tsc" && _status == "pending" &&
            _pendingOperationId == e.OperationId && e.Label == _requestedLabel && e.LabelGeneration > 0 && e.ReservationEventId != Uuid.Empty,
            "Invalid draft activation history.");
        RecordParticipant(e.OperationId, "activate", e.ReservationEventId, e.LabelGeneration);
        _label = e.Label;
        _labelGeneration = e.LabelGeneration;
        _currentLabelOperationId = e.OperationId;
        _status = "activating";
    }

    void ApplyDrafted(CriteriaEditionDrafted e)
    {
        Require(e.EditionId == _editionId && e.DraftGeneration == _draftGeneration && e.Framework == _framework && _framework == "tsc" && _status == "activating" &&
            _pendingOperationId == e.OperationId && e.Label == _label && e.LabelGeneration == _labelGeneration && e.ConfirmationEventId != Uuid.Empty,
            "Invalid draft confirmation history.");
        RecordParticipant(e.OperationId, "drafted", e.ConfirmationEventId, e.LabelGeneration);
        _lastLabelConfirmationEventId = e.ConfirmationEventId;
        SetOutcome(e.OperationId, "accepted", null);
        _pendingOperationId = Uuid.Empty;
        _requestedLabel = null;
        _status = "draft";
    }

    void ApplyDraftCancelled(CriteriaEditionDraftRequestCancelled e)
    {
        var digest = AuthoredIntentDigest.Compute(new CancelCriteriaEditionDraftRequest(_editionId, e.OperationId, e.TargetOperationId));
        Require(e.EditionId == _editionId && e.OperationId != e.TargetOperationId && _status is ("pending" or "needs_label" or "activating") &&
            _pendingOperationId == e.TargetOperationId && ValidReplayAdmission(e.OperationId, e.ExecutionGeneration, e.Actor, e.Admission, digest) &&
            !IsRecorded(e.OperationId), "Invalid draft cancellation history.");
        Record(e.OperationId, new("cancel-draft", digest, e.Actor, "accepted", null));
        if (Selected(e.TargetOperationId) && _selectedOperation is { Status: "pending" })
            SetOutcome(e.TargetOperationId, "refused", "cancelled");
        _status = "abandoned";
        _pendingOperationId = Uuid.Empty;
    }

    void ApplyRenameRequested(CriteriaEditionRenameRequested e)
    {
        var digest = AuthoredIntentDigest.Compute(new BeginCriteriaEditionRename(_editionId, e.OperationId, e.NewLabel));
        Require(e.EditionId == _editionId && _status == "draft" && e.OldLabel == _label && e.OldLabelGeneration == _labelGeneration &&
            EditionLabelClaim.TryNormalize(e.NewLabel, out var normalized) && normalized == e.NewLabel && e.NewLabel != e.OldLabel &&
            ValidReplayAdmission(e.OperationId, e.ExecutionGeneration, e.Actor, e.Admission, digest) && !IsRecorded(e.OperationId),
            "Invalid edition rename history.");
        Record(e.OperationId, new("rename", digest, e.Actor, "pending", null));
        _pendingParticipantAdmissionCurrent = AccessAdmissionEvidenceBinding.IsCurrentFormat(e.Admission);
        _pendingOperationId = e.OperationId;
        _requestedLabel = e.NewLabel;
        _status = "renaming";
    }

    void ApplyDraftRenamed(CriteriaEditionDraftRenamed e)
    {
        Require(e.EditionId == _editionId && _status == "renaming" && _pendingOperationId == e.OperationId &&
            e.OldLabel == _label && e.OldLabelGeneration == _labelGeneration && e.NewLabel == _requestedLabel &&
            e.NewLabelGeneration > 0 && e.ReservationEventId != Uuid.Empty, "Invalid edition rename commit history.");
        RecordParticipant(e.OperationId, "rename-commit", e.ReservationEventId, e.NewLabelGeneration);
        _label = e.NewLabel;
        _labelGeneration = e.NewLabelGeneration;
        _currentLabelOperationId = e.OperationId;
        _status = "confirming";
    }

    void ApplyRenameCompleted(CriteriaEditionRenameCompleted e)
    {
        Require(e.EditionId == _editionId && _status == "confirming" && _pendingOperationId == e.OperationId &&
            e.Label == _label && e.LabelGeneration == _labelGeneration && e.ConfirmationEventId != Uuid.Empty, "Invalid edition rename confirmation history.");
        RecordParticipant(e.OperationId, "rename-complete", e.ConfirmationEventId);
        _lastLabelConfirmationEventId = e.ConfirmationEventId;
        SetOutcome(e.OperationId, "accepted", null);
        _pendingOperationId = Uuid.Empty;
        _requestedLabel = null;
        _status = "draft";
    }

    void ApplyRenameRefused(CriteriaEditionRenameRefused e)
    {
        Require(e.EditionId == _editionId && _status == "renaming" && _pendingOperationId == e.OperationId &&
            e.RefusalEventId != Uuid.Empty && !string.IsNullOrWhiteSpace(e.Reason), "Invalid edition rename refusal history.");
        RecordParticipant(e.OperationId, "rename-refusal", e.RefusalEventId, reason: e.Reason);
        SetOutcome(e.OperationId, "refused", e.Reason);
        _pendingOperationId = Uuid.Empty;
        _requestedLabel = null;
        _status = "draft";
    }

    void ApplyRenameCancelled(CriteriaEditionRenameCancelled e)
    {
        var digest = AuthoredIntentDigest.Compute(new CancelCriteriaEditionRename(_editionId, e.OperationId, e.RenameOperationId));
        Require(e.EditionId == _editionId && e.OperationId != e.RenameOperationId && _status == "renaming" && _pendingOperationId == e.RenameOperationId &&
            ValidReplayAdmission(e.OperationId, e.ExecutionGeneration, e.Actor, e.Admission, digest) && !IsRecorded(e.OperationId),
            "Invalid edition rename cancellation history.");
        Record(e.OperationId, new("cancel-rename", digest, e.Actor, "accepted", null));
        SetOutcome(e.RenameOperationId, "refused", "cancelled");
        _pendingOperationId = Uuid.Empty;
        _requestedLabel = null;
        _status = "draft";
    }

    void ApplyPublicationStarted(CriteriaPublicationStarted e)
    {
        Require(e.EditionId == _editionId && _status == "draft" && e.DraftGeneration == _draftGeneration &&
            e.PublicationAttemptId != Uuid.Empty && e.ExecutionGeneration >= _executionFloor && e.AttemptOrdinal == _attemptOrdinal + 1 &&
            !string.IsNullOrWhiteSpace(e.WitnessIdentifier) && e.WitnessIncarnationId != Uuid.Empty && e.Label == _label &&
            e.LabelGeneration == _labelGeneration && e.LabelConfirmationEventId == _lastLabelConfirmationEventId &&
            e.LabelConfirmationEventId != Uuid.Empty && !IsRecorded(e.PublicationAttemptId), "Invalid publication attempt history.");
        if (e.Admission is null)
        {
            Require(IsSeedBound && e.Actor == new ActorAttribution("criteria-seed-bootstrap", "system"), "Only trusted seed may omit human admission.");
            Record(e.PublicationAttemptId, new("seed-publish", e.WitnessIdentifier + ":" + e.WitnessIncarnationId, e.Actor, "pending", null));
        }
        else
        {
            var digest = AuthoredIntentDigest.Compute(new BeginCriteriaPublication(_editionId, e.PublicationAttemptId, e.WitnessIdentifier, e.WitnessIncarnationId));
            Require(ValidReplayAdmission(e.PublicationAttemptId, e.ExecutionGeneration, e.Actor, e.Admission, digest), "Invalid publication admission.");
            Record(e.PublicationAttemptId, new("publish", digest, e.Actor, "pending", null));
        }
        _attempt = e;
        _attemptOrdinal = e.AttemptOrdinal;
        _pendingOperationId = e.PublicationAttemptId;
        _status = "publishing";
    }

    void ApplyPublicationAborted(CriteriaPublicationAborted e)
    {
        Require(e.EditionId == _editionId && _status == "publishing" && _attempt is not null &&
            e.DraftGeneration == _draftGeneration && e.PublicationAttemptId == _attempt.PublicationAttemptId && e.AttemptOrdinal == _attempt.AttemptOrdinal &&
            e.WitnessIdentifier == _attempt.WitnessIdentifier && e.WitnessIncarnationId == _attempt.WitnessIncarnationId && !string.IsNullOrWhiteSpace(e.Reason),
            "Invalid publication abort history.");
        if (Selected(e.PublicationAttemptId))
            _selectedAbort = e;
        SetOutcome(e.PublicationAttemptId, "refused", e.Reason);
        _status = "abort_cleanup";
    }

    void ApplyAbortCleanup(CriteriaPublicationAbortCleanupCompleted e)
    {
        Require(e.EditionId == _editionId && _status == "abort_cleanup" && _attempt is not null &&
            e.DraftGeneration == _draftGeneration && e.PublicationAttemptId == _attempt.PublicationAttemptId && e.AttemptOrdinal == _attempt.AttemptOrdinal &&
            e.WitnessIdentifier == _attempt.WitnessIdentifier && e.WitnessIncarnationId == _attempt.WitnessIncarnationId && e.WitnessFenceEventId != Uuid.Empty,
            "Invalid publication abort cleanup history.");
        RecordParticipant(e.PublicationAttemptId, "abort-cleanup", e.WitnessFenceEventId);
        _attempt = null;
        _pendingOperationId = Uuid.Empty;
        _status = "draft";
    }

    void ApplyPublished(CriteriaEditionPublished e)
    {
        Require(e.EditionId == _editionId && _status == "publishing" && _attempt is not null &&
            e.DraftGeneration == _draftGeneration && e.PublicationAttemptId == _attempt.PublicationAttemptId && e.AttemptOrdinal == _attempt.AttemptOrdinal &&
            e.WitnessIdentifier == _attempt.WitnessIdentifier && e.WitnessIncarnationId == _attempt.WitnessIncarnationId && e.Label == _label &&
            e.LabelGeneration == _labelGeneration && e.LabelConfirmationEventId == _lastLabelConfirmationEventId &&
            e.WitnessPinEventId != Uuid.Empty && e.PublishedBy == _attempt.Actor, "Invalid edition publication history.");
        _publication = e;
        SetOutcome(e.PublicationAttemptId, "accepted", null);
        _pendingOperationId = Uuid.Empty;
        _status = "published";
    }

    void ApplyAbandoned(CriteriaEditionDraftAbandoned e)
    {
        var digest = AuthoredIntentDigest.Compute(new AbandonCriteriaEditionDraft(_editionId, e.OperationId));
        Require(e.EditionId == _editionId && _status == "draft" && e.DraftGeneration == _draftGeneration && e.Label == _label &&
            e.LabelGeneration == _labelGeneration && ValidReplayAdmission(e.OperationId, e.ExecutionGeneration, e.Actor, e.Admission, digest) &&
            !IsRecorded(e.OperationId), "Invalid edition abandonment history.");
        Record(e.OperationId, new("abandon", digest, e.Actor, "accepted", null));
        _status = "abandoned";
    }

    bool ValidReplayAdmission(Uuid operationId, long generation, ActorAttribution actor, AccessAdmissionEvidence admission, string digest) =>
        operationId != Uuid.Empty && generation >= _executionFloor && actor is not null && admission is not null &&
        AccessAdmissionEvidenceBinding.IsHistoricalFormat(admission) &&
        admission.UserId != Uuid.Empty && admission.OperationId == operationId && admission.Target == Stream.ToString() &&
        admission.Operator is not null && admission.Identity is not null && admission.Policy is not null &&
        actor.Subject == admission.ActorSubject && actor.Issuer == admission.ActorIssuer &&
        admission.IntentSha256 == BindAdmissionDigest(digest, generation) &&
        (!Selected(operationId) || _selectedFence is not { } fence || (fence.Intent == digest && generation > fence.Generation));

    bool Selected(Uuid operationId) => operationId != Uuid.Empty && operationId == _selectedOperationId;
    bool IsRecorded(Uuid operationId) => Selected(operationId) && _selectedOperation is not null;

    void Record(Uuid operationId, Operation operation)
    {
        if (Selected(operationId))
            _selectedOperation = operation;
    }

    void RecordParticipant(Uuid operationId, string stage, Uuid eventId, long generation = 0, string? reason = null)
    {
        if (Selected(operationId))
            _selectedParticipantFacts.Add(stage, new ParticipantOutcome(eventId, generation, reason));
    }

    void SetOutcome(Uuid operationId, string status, string? reason)
    {
        if (!Selected(operationId))
            return;
        var previous = _selectedOperation ?? throw new InvalidOperationException("The selected operation has no source intent.");
        _selectedOperation = previous with { Status = status, Reason = reason };
    }

    sealed record Operation(string Kind, string Digest, ActorAttribution Actor, string Status, string? Reason);
    sealed record ParticipantOutcome(Uuid EventId, long Generation, string? Reason);
}
