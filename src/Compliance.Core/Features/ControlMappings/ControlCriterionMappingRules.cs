using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     The mapping line of one (program, control, edition, criterion) tuple: one pending proposal at a time and one
///     accepted version, where an independently approved successor supersedes exactly the current accepted version and a
///     retirement can only remove the version it names. Its identity derives from the tuple only, so different tuples
///     never share a stream and never conflict; its logical revision counts the facts of this line. Each proposal is
///     named by its own mapping id, which is the logical operation identity of the proposal.
/// </summary>
public sealed class ControlCriterionMappingRules
{
    /// <summary>Stream area of every mapping line (the realm is the tenant).</summary>
    public const string Area = "control-criterion-mappings";

    /// <summary>The longest rationale or explanation, in characters.</summary>
    public const int MaximumTextLength = 4000;

    /// <summary>Schema of the registration a proposal returns.</summary>
    public const string RegistrationSchema = "ControlCriterionMapping.Registration.v1";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _controlId;
    readonly Uuid _editionId;
    readonly string _criterionIdentifier;
    readonly List<object> _facts = [];
    readonly List<VersionState> _versions = [];
    ControlCriterionMappingView? _view;
    long _revision;

    /// <summary>Opens the mapping line of one tuple, for transient business validation and explicit history composition.</summary>
    public ControlCriterionMappingRules(Uuid tenantId, Uuid programId, Uuid controlId, Uuid editionId,
        string criterionIdentifier)
    {
        (_tenantId, _programId, _controlId, _editionId, _criterionIdentifier) =
            (tenantId, programId, controlId, editionId, criterionIdentifier);
    }

    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _programId;
    public Uuid EditionId => _editionId;
    public string CriterionIdentifier => _criterionIdentifier;
    public Uuid ControlId => _controlId;
    public Uuid Id => IdentityOf(_tenantId, _programId, _controlId, _editionId, _criterionIdentifier);
    public EventStreamAddress Stream => new(_tenantId.ToString(), Area, Id.ToString());

    /// <summary>The logical revision: the number of facts on this line.</summary>
    public long Revision => _revision;

    /// <summary>Every fact of the line in order.</summary>
    public IReadOnlyList<object> Changes => _facts;

    /// <summary>The line's public view, or null before the first proposal.</summary>
    public ControlCriterionMappingView? View => _view;

    /// <summary>The identity of the mapping line of one tuple.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid controlId, Uuid editionId,
        string criterionIdentifier) => AggregateIdentity.Create("ControlCriterionMapping", tenantId, programId,
        controlId, editionId, criterionIdentifier);

    /// <summary>Whether a proposal with this id is on the line.</summary>
    public bool Contains(Uuid mappingId) => _versions.Exists(version => version.MappingId == mappingId);

    /// <summary>Checks the proposal guards without raising anything.</summary>
    public Result PrepareProposal(Uuid controlVersionId, long expectedRevision, string rationale,
        string applicabilityExplanation)
    {
        if (controlVersionId == Uuid.Empty || !IsBoundedText(rationale) || !IsBoundedText(applicabilityExplanation))
            return Fail(CommandFailure.InvalidContent(
                "A mapping requires a control version, catalog entry, rationale, and applicability explanation of at most 4000 characters each."));
        if (Pending is not null && expectedRevision == _revision)
            return Fail(CommandFailure.StateConflict("The mapping already has a pending proposal awaiting review."));
        if (expectedRevision != _revision)
            return Stale();
        if (Active is { } active && active.Matches(controlVersionId, rationale.Trim(), applicabilityExplanation.Trim()))
            return Fail(CommandFailure.StateConflict(
                "An accepted mapping with the same control version and explanation already exists."));
        return Result.Success;
    }

    /// <summary>Proposes a mapping version as the root native fact of its operation.</summary>
    public Result Propose(OperationProvenance operation, Uuid controlVersionId, string criterionKind, string rationale,
        string applicabilityExplanation, Uuid actorMemberId, string actorDisplay, DateTimeOffset proposedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A proposal is a root operation of its own mapping line with an expected revision."));
        var prepared = PrepareProposal(controlVersionId, (long)expected, rationale, applicabilityExplanation);
        if (!prepared.IsSuccess)
            return prepared;
        Apply(new ControlCriterionMappingProposedChange(_tenantId, _programId, operation.LogicalId, _revision + 1,
            _versions.Count + 1, _controlId, controlVersionId, _editionId, _criterionIdentifier, criterionKind,
            rationale.Trim(), applicabilityExplanation.Trim(), actorMemberId, actorDisplay, proposedAt, operation));
        return Result.Success;
    }

    /// <summary>
    ///     Checks the review guards without raising anything. The proposer may review only under an approved waiver
    ///     scoped to (control_criterion_mapping, mapping_id, mapping_id, expected_revision, review).
    /// </summary>
    public Result PrepareReview(Uuid mappingId, long expectedRevision, string outcome, string rationale,
        Uuid reviewerMemberId, DateTimeOffset decidedAt, SeparationOfDutiesWaiver? waiver)
    {
        if (Pending is not { } pending)
            return Fail(CommandFailure.StateConflict("The mapping has no pending proposal to review."));
        if (pending.MappingId != mappingId)
            return Fail(CommandFailure.StateConflict("The proposal is not the pending proposal of its mapping."));
        if (expectedRevision != _revision)
            return Stale();
        if (waiver is not null && (waiver.TenantId != _tenantId || !waiver.Allows(
                new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.ControlCriterionMapping, mappingId,
                    mappingId, expectedRevision, SeparationOfDutiesActions.Review), reviewerMemberId, decidedAt)))
            return Fail(CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and mapping revision."));
        if (pending.ProposerMemberId == reviewerMemberId && waiver is null)
            return Fail(CommandFailure.ActorProhibited("A mapping proposer cannot review their own proposal."));
        if (outcome is not ("accept" or "reject") || !IsBoundedText(rationale))
            return Fail(CommandFailure.InvalidContent(
                "A mapping review requires an outcome of accept or reject and a rationale of at most 4000 characters."));
        return Result.Success;
    }

    /// <summary>Reviews the pending proposal as the root native fact of its operation.</summary>
    public Result Review(OperationProvenance operation, Uuid mappingId, string outcome, string rationale,
        Uuid reviewerMemberId, string reviewerDisplay, DateTimeOffset decidedAt, SeparationOfDutiesWaiver? waiver)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A review is a root operation of its own mapping line with an expected revision."));
        var prepared = PrepareReview(mappingId, (long)expected, outcome, rationale, reviewerMemberId, decidedAt, waiver);
        if (!prepared.IsSuccess)
            return prepared;
        Apply(new ControlCriterionMappingReviewedChange(_tenantId, _programId, mappingId, _revision + 1,
            Pending!.Number, operation.LogicalId, outcome, rationale.Trim(), reviewerMemberId, reviewerDisplay,
            decidedAt, waiver?.Id, _controlId, _editionId, _criterionIdentifier, operation));
        return Result.Success;
    }

    /// <summary>Checks the retirement guards without raising anything.</summary>
    public Result PrepareRetirement(Uuid mappingId, long expectedRevision, string rationale)
    {
        if (_versions.Count == 0)
            return Fail(CommandFailure.MissingRecord("The control criterion mapping was not found."));
        if (expectedRevision != _revision)
            return Stale();
        if (Pending is not null)
            return Fail(CommandFailure.StateConflict("Review the pending proposal before retiring the mapping."));
        if (Active is not { } active)
            return Fail(CommandFailure.StateConflict("The mapping has no accepted version to retire."));
        if (active.MappingId != mappingId)
            return Fail(CommandFailure.StateConflict(
                "The mapping is not the accepted version of its line; a newer version cannot be retired by an older mapping."));
        if (!IsBoundedText(rationale))
            return Fail(CommandFailure.InvalidContent(
                "Retiring a mapping requires a rationale of at most 4000 characters."));
        return Result.Success;
    }

    /// <summary>Retires the accepted mapping as the root native fact of its operation.</summary>
    public Result Retire(OperationProvenance operation, Uuid mappingId, string rationale, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset retiredAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A retirement is a root operation of its own mapping line with an expected revision."));
        var prepared = PrepareRetirement(mappingId, (long)expected, rationale);
        if (!prepared.IsSuccess)
            return prepared;
        Apply(new ControlCriterionMappingRetiredChange(_tenantId, _programId, mappingId, _revision + 1,
            Active!.Number, rationale.Trim(), actorMemberId, actorDisplay, retiredAt, _controlId, _editionId,
            _criterionIdentifier, operation));
        return Result.Success;
    }

    VersionState? Pending => _versions.Find(static version => version.Status == "proposed");

    VersionState? Active => _versions.Find(static version => version.Status == "accepted");

    public void Apply(ControlCriterionMappingProposedChange ev)
    {
        if (!Owns(ev.TenantId, ev.ProgramId, ev.ControlId, ev.EditionId, ev.CriterionIdentifier) ||
            ev.Revision != _revision + 1 || ev.VersionNumber != _versions.Count + 1 || Pending is not null ||
            ev.Operation.Owner != Stream || ev.Operation.LogicalId != ev.MappingId ||
            Contains(ev.MappingId))
            throw new InvalidOperationException(
                "A mapping proposal follows the previous revision of its own line while nothing is pending.");
        _versions.Add(new VersionState(ev.VersionNumber, ev.MappingId, ev.ControlVersionId, ev.ActorMemberId,
            ev.Rationale, ev.ApplicabilityExplanation));
        Fold(ev);
    }

    public void Apply(ControlCriterionMappingReviewedChange ev)
    {
        if (!Owns(ev.TenantId, ev.ProgramId, ev.ControlId, ev.EditionId, ev.CriterionIdentifier) ||
            ev.Revision != _revision + 1 || Pending is not { } pending || pending.MappingId != ev.MappingId ||
            pending.Number != ev.VersionNumber || ev.Operation.Owner != Stream)
            throw new InvalidOperationException(
                "A mapping review follows the previous revision of its own line and names the pending proposal.");
        if (ev.Outcome == "accept")
        {
            if (Active is { } previous)
                previous.Status = "superseded";
            pending.Status = "accepted";
        }
        else
        {
            pending.Status = "rejected";
        }

        Fold(ev);
    }

    public void Apply(ControlCriterionMappingRetiredChange ev)
    {
        if (!Owns(ev.TenantId, ev.ProgramId, ev.ControlId, ev.EditionId, ev.CriterionIdentifier) ||
            ev.Revision != _revision + 1 || Pending is not null || Active is not { } active ||
            active.MappingId != ev.MappingId || active.Number != ev.VersionNumber || ev.Operation.Owner != Stream)
            throw new InvalidOperationException(
                "A mapping retirement follows the previous revision of its own line and names the accepted version.");
        active.Status = "retired";
        Fold(ev);
    }

    public void Apply(object change)
    {
        switch (change)
        {
            case ControlCriterionMappingProposedChange value:
                Apply(value);
                break;
            case ControlCriterionMappingReviewedChange value:
                Apply(value);
                break;
            case ControlCriterionMappingRetiredChange value:
                Apply(value);
                break;
            default:
                throw new InvalidOperationException("The immutable change has another mapping family.");
        }
    }

    void Fold(object ev)
    {
        _view = ControlCriterionMappingReducer.Apply(_view, ev);
        _revision = _view.Revision;
        _facts.Add(ev);
    }

    bool Owns(Uuid tenantId, Uuid programId, Uuid controlId, Uuid editionId, string criterionIdentifier) =>
        tenantId == _tenantId && programId == _programId && controlId == _controlId && editionId == _editionId &&
        string.Equals(criterionIdentifier, _criterionIdentifier, StringComparison.Ordinal);

    static bool IsBoundedText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= MaximumTextLength;

    static Result Fail(CommandFailure failure) => Result.Failure(failure.ToRequestError());

    Result Stale() => Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
        "control criterion mapping", _revision)));

    sealed class VersionState(int number, Uuid mappingId, Uuid controlVersionId, Uuid proposerMemberId,
        string rationale, string explanation)
    {
        public int Number { get; } = number;
        public Uuid MappingId { get; } = mappingId;
        public Uuid ProposerMemberId { get; } = proposerMemberId;
        public string Status { get; set; } = "proposed";

        public bool Matches(Uuid otherControlVersionId, string otherRationale, string otherExplanation) =>
            controlVersionId == otherControlVersionId && StringComparer.Ordinal.Equals(rationale, otherRationale) &&
            StringComparer.Ordinal.Equals(explanation, otherExplanation);
    }
}
