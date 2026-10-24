using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The business rules of operating plans and occurrences, decided over the folded state of one plan line or one
///     occurrence. The owners re-check only the logical revision when they append.
/// </summary>
public static class ControlOperationRules
{
    const int MaximumTextLength = 4000;
    const int MaximumEvidence = 50;

    /// <summary>The proposal rules over the line, the approved control version and the reviewer's conflict.</summary>
    public static CommandFailure? CheckProposal(ControlOperatingPlanLine line, Uuid tenantId, long expectedRevision,
        ControlVersionView version, OperatingHolder owner, OperatingHolder? backupOwner, Uuid reviewerMemberId,
        bool reviewerHoldsWork, ControlCadence cadence, DateOnly effectiveFrom, string rationale,
        DateTimeOffset proposedAt, SeparationOfDutiesWaiver? waiver)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(version);
        if (expectedRevision != line.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("control operating plan",
                line.Revision));
        if (line.Pending is not null)
            return CommandFailure.StateConflict("An operating plan is already pending approval for this control.");
        if (ValidatePlan(version, owner, backupOwner, reviewerMemberId, cadence, effectiveFrom, line.Current) is
            { } invalid)
            return invalid;
        if (!IsBoundedText(rationale))
            return CommandFailure.InvalidContent("An operating plan requires a rationale of at most 4000 characters.");
        if (reviewerHoldsWork)
        {
            var scope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.ControlOperatingPlan,
                line.ControlId, version.VersionId, expectedRevision + 1, SeparationOfDutiesActions.Review);
            if (waiver is null || waiver.TenantId != tenantId || !waiver.Allows(scope, reviewerMemberId, proposedAt))
                return CommandFailure.StateConflict(
                    "The reviewer also holds the control's operating work and requires an approved, active SoD waiver.");
        }
        else if (waiver is not null)
            return CommandFailure.InvalidContent(
                "A separation-of-duties waiver may only be used for a current conflict.");
        return null;
    }

    /// <summary>The structural rules shared by a proposal and its preview.</summary>
    public static CommandFailure? ValidatePlan(ControlVersionView version, OperatingHolder? owner,
        OperatingHolder? backupOwner, Uuid reviewerMemberId, ControlCadence? cadence, DateOnly effectiveFrom,
        ControlOperatingPlanView? current)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.Status != ControlOperationStates.Approved)
            return CommandFailure.StateConflict("An operating plan must target the control's current approved version.");
        if (!IsHolder(owner) || backupOwner is not null && !IsHolder(backupOwner) || backupOwner == owner ||
            reviewerMemberId == Uuid.Empty)
            return CommandFailure.InvalidContent(
                "An operating plan requires an owner, an optional distinct backup owner, and a reviewer member. A holder kind is member, person, or team.");
        if (ControlCadenceSchedule.Validate(cadence) is { } cadenceError)
            return CommandFailure.InvalidContent(cadenceError);
        if (cadence!.FirstPeriodStart is { } first && first < effectiveFrom)
            return CommandFailure.InvalidContent("The first cadence period cannot start before the plan's effective date.");
        if (effectiveFrom == default || effectiveFrom < version.EffectiveFrom ||
            version.EffectiveUntil is { } until && effectiveFrom >= until)
            return CommandFailure.InvalidContent(
                "The plan's effective date must fall inside the control version's effective interval.");
        return current is not null && effectiveFrom <= current.EffectiveFrom
            ? CommandFailure.InvalidContent("A revised plan must take effect after the current plan's effective date.")
            : null;
    }

    /// <summary>The approval rules over the line and the approver's independence.</summary>
    public static CommandFailure? CheckApproval(ControlOperatingPlanLine line, Uuid tenantId, long expectedRevision,
        Uuid planVersionId, string rationale, Uuid approverMemberId, DateTimeOffset approvedAt,
        SeparationOfDutiesWaiver? waiver)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Find(planVersionId) is not { } plan)
            return CommandFailure.MissingRecord("The operating plan was not found.");
        if (plan.Status != ControlOperationStates.PendingApproval)
            return CommandFailure.StateConflict("The operating plan is not pending approval.");
        if (expectedRevision != line.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("control operating plan",
                line.Revision));
        var scope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.ControlOperatingPlan,
            line.ControlId, planVersionId, plan.Revision, SeparationOfDutiesActions.Approve);
        if (waiver is not null && (waiver.TenantId != tenantId || !waiver.Allows(scope, approverMemberId, approvedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and plan revision.");
        if (plan.ProposerMemberId == approverMemberId && waiver is null)
            return CommandFailure.ActorProhibited("The member who proposed an operating plan cannot approve it.");
        return IsBoundedText(rationale)
            ? null
            : CommandFailure.InvalidContent("An operating plan approval requires a rationale of at most 4000 characters.");
    }

    /// <summary>The rules for opening an event-driven or ad hoc occurrence on the approved plan in force.</summary>
    public static CommandFailure? CheckOpening(ControlOperatingPlanView? plan,
        IReadOnlyDictionary<Uuid, DateOnly?> versionUntil, string trigger, DateOnly occurredOn,
        ControlBusinessWindow window, DateTimeOffset openedAt)
    {
        ArgumentNullException.ThrowIfNull(versionUntil);
        if (plan is null)
            return CommandFailure.StateConflict("The control has no approved operating plan effective on that date.");
        if (plan.Cadence.Kind == ControlCadenceSchedule.Recurring)
            return CommandFailure.StateConflict("A recurring control operates through its expected occurrences.");
        if (!IsActive(versionUntil, plan.ControlVersionId, occurredOn))
            return CommandFailure.StateConflict("The control version is not effective on that date.");
        if (string.IsNullOrWhiteSpace(trigger) || trigger.Trim().Length > MaximumTextLength ||
            occurredOn > DateOnly.FromDateTime(openedAt.UtcDateTime) || window is null || !window.IsValid)
            return CommandFailure.InvalidContent(
                "An occurrence requires a trigger of at most 4000 characters, a valid business window and a date that is not in the future.");
        return null;
    }

    /// <summary>The attestation rules over the occurrence as the caller read it.</summary>
    public static CommandFailure? CheckAttestation(ControlOccurrenceView current, long expectedRevision,
        AttestationInput input, IReadOnlyList<string> expectedEvidence, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(input);
        var today = DateOnly.FromDateTime(recordedAt.UtcDateTime);
        if (current.Attestations.Count > 0)
            return CommandFailure.StateConflict(
                "The occurrence already has an attestation. Correct it by a new version instead.");
        if (expectedRevision != current.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("control occurrence", current.Revision));
        if (current.PeriodStart is { } start && start > today)
            return CommandFailure.StateConflict("An expected occurrence cannot be attested before its period starts.");
        return ValidateAttestation(input, expectedEvidence, recordedAt);
    }

    /// <summary>The correction rules over the occurrence as the caller read it.</summary>
    public static CommandFailure? CheckCorrection(ControlOccurrenceView current, long expectedRevision,
        AttestationInput input, string correctionReason, IReadOnlyList<string> expectedEvidence,
        DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(input);
        if (current.Attestations.Count == 0)
            return CommandFailure.StateConflict("The occurrence has no attestation to correct.");
        if (expectedRevision != current.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("control occurrence", current.Revision));
        if (!IsBoundedText(correctionReason))
            return CommandFailure.InvalidContent("A correction requires a reason of at most 4000 characters.");
        return ValidateAttestation(input, expectedEvidence, recordedAt);
    }

    /// <summary>The review rules over the occurrence as the caller read it.</summary>
    public static CommandFailure? CheckReview(ControlOccurrenceView current, Uuid tenantId, long expectedRevision,
        Uuid attestationId, string outcome, string rationale, IReadOnlyList<string> requestedActions,
        Uuid reviewerMemberId, DateTimeOffset reviewedAt, SeparationOfDutiesWaiver? waiver)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(requestedActions);
        if (expectedRevision != current.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("control occurrence", current.Revision));
        if (current.State is not (ControlOperationStates.Submitted or ControlOperationStates.Deferred))
            return CommandFailure.StateConflict("The occurrence has no attestation awaiting review.");
        var attestation = current.Attestations[^1];
        if (attestation.AttestationId != attestationId)
            return CommandFailure.StateConflict("Review the latest attestation version.");
        var scope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.ControlOccurrence,
            current.OccurrenceId, attestationId, attestation.Version, SeparationOfDutiesActions.Review);
        if (waiver is not null && (waiver.TenantId != tenantId || !waiver.Allows(scope, reviewerMemberId, reviewedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and attestation.");
        var performedOrRecorded = attestation.RecorderMemberId == reviewerMemberId ||
                                  attestation.PerformedBy is { Kind: "member" } performer &&
                                  performer.Id == reviewerMemberId;
        if (performedOrRecorded && waiver is null)
            return CommandFailure.ActorProhibited("The member who performed or recorded an attestation cannot review it.");
        if (outcome is not (ControlOperationStates.Approved or ControlOperationStates.Returned or
                ControlOperationStates.ActionRequested or ControlOperationStates.Deferred) ||
            !IsBoundedText(rationale))
            return CommandFailure.InvalidContent(
                "A review requires an outcome of approved, returned, action_requested, or deferred and a rationale of at most 4000 characters.");
        if (outcome == ControlOperationStates.ActionRequested
                ? requestedActions.Count is < 1 or > 20 || requestedActions.Any(static action => !IsBoundedText(action))
                : requestedActions.Count > 0)
            return CommandFailure.InvalidContent(
                "Requested actions are required for action_requested, at most 20 of at most 4000 characters, and not allowed otherwise.");
        return null;
    }

    /// <summary>The immutable attestation version a proof records.</summary>
    public static ControlAttestationView BuildAttestation(Uuid occurrenceId, int version, AttestationInput input,
        ControlOperatingPlanView plan, DateOnly? periodStart, DateOnly? periodEnd, string? correctionReason,
        Uuid? supersedes, Uuid recorderMemberId, string recorderDisplay, DateTimeOffset recordedAt) => new(
        Uuid.CreateVersion5(occurrenceId, "attestation-" + version.ToString(CultureInfo.InvariantCulture)),
        version, input.Result, input.PerformedAt, input.CoveredFrom ?? periodStart, input.CoveredUntil ?? periodEnd,
        Trim(input.Notes), Trim(input.Rationale),
        input.Evidence.Select(static evidence => new EvidenceReference(evidence.ExpectedEvidenceIndex, evidence.Kind,
            evidence.Reference.Trim(), Trim(evidence.Description), "unresolved")).ToArray(),
        input.PerformedBy, recorderMemberId, ActorReference.ForMember(recorderMemberId, recorderDisplay), recordedAt,
        plan.ControlVersionId, plan.PlanVersionId, plan.ExpectedEvidence, correctionReason, supersedes);

    /// <summary>The attestation content rules.</summary>
    public static CommandFailure? ValidateAttestation(AttestationInput input, IReadOnlyList<string> expectedEvidence,
        DateTimeOffset recordedAt)
    {
        if (input.Result is not (ControlOperationStates.Complete or ControlOperationStates.Failed or
                ControlOperationStates.NotApplicable or ControlOperationStates.Skipped))
            return CommandFailure.InvalidContent(
                "An attestation result must be complete, failed, not_applicable, or skipped.");
        if (input.PerformedBy.Kind is not ("member" or "person"))
            return CommandFailure.InvalidContent("The performer of record must be a member or a workforce person.");
        if (input.PerformedAt == default || input.PerformedAt > recordedAt)
            return CommandFailure.InvalidContent("The performed time cannot be in the future.");
        if (input.CoveredFrom is { } from && input.CoveredUntil is { } until && until < from)
            return CommandFailure.InvalidContent("The covered period must end on or after it starts.");
        if (input.Notes is { } notes && notes.Trim().Length > MaximumTextLength)
            return CommandFailure.InvalidContent("Attestation notes must be at most 4000 characters.");
        if (input.Result != ControlOperationStates.Complete && !IsBoundedText(input.Rationale))
            return CommandFailure.InvalidContent(
                "A failed, not_applicable, or skipped result requires a rationale of at most 4000 characters.");
        if (input.Evidence.Count > MaximumEvidence || input.Evidence.Any(evidence =>
                evidence is null || evidence.Kind is not ("artifact" or "record" or "external") ||
                string.IsNullOrWhiteSpace(evidence.Reference) || evidence.Reference.Length > 2000 ||
                evidence.Description is { Length: > 2000 } ||
                evidence.ExpectedEvidenceIndex is { } index && (index < 0 || index >= expectedEvidence.Count)))
            return CommandFailure.InvalidContent(
                "Each evidence reference needs a kind of artifact, record, or external, a reference of at most 2000 characters, and a valid expected evidence index.");
        if (input.Result != ControlOperationStates.Complete)
            return null;
        var covered = input.Evidence.Where(static evidence => evidence.ExpectedEvidenceIndex is not null)
            .Select(static evidence => evidence.ExpectedEvidenceIndex!.Value).ToHashSet();
        return input.Evidence.Count == 0 ||
               Enumerable.Range(0, expectedEvidence.Count).Any(index => !covered.Contains(index))
            ? CommandFailure.InvalidContent("A complete result requires support for every expected evidence item.")
            : null;
    }

    /// <summary>Whether a control version is still effective on the date.</summary>
    public static bool IsActive(IReadOnlyDictionary<Uuid, DateOnly?> versionUntil, Uuid versionId, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(versionUntil);
        return !versionUntil.TryGetValue(versionId, out var until) || until is null || date < until;
    }

    /// <summary>The earlier of two optional dates.</summary>
    public static DateOnly? Earliest(DateOnly? first, DateOnly? second) =>
        first is null ? second : second is null ? first : first < second ? first : second;

    /// <summary>Non-blank text of at most 4000 characters.</summary>
    public static bool IsBoundedText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= MaximumTextLength;

    /// <summary>Trimmed text, or null when blank.</summary>
    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static bool IsHolder(OperatingHolder? holder) => holder is not null && holder.Id != Uuid.Empty &&
        holder.Kind is "member" or "person" or "team";
}
