using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.ControlMappings.Services;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.ControlMappings.Rules;

/// <summary>
///     The applicability line of one (program, edition, criterion): at most one pending not-applicable proposal, and
///     only an independently accepted review makes the criterion not applicable until a withdrawal restores coverage
///     while keeping the earlier facts. Its identity derives from the program, edition and criterion only, so
///     different criteria never share a stream and never conflict; its logical revision counts the facts of this line.
///     Each proposal is named by its own decision id, which is the logical operation identity of the proposal.
/// </summary>
public sealed class CriterionApplicabilityRules
{
    /// <summary>Stream area of every applicability line (the realm is the tenant).</summary>
    public const string Area = "criterion-applicability";

    /// <summary>The longest rationale, in characters.</summary>
    public const int MaximumTextLength = 4000;

    /// <summary>Schema of the registration a proposal returns.</summary>
    public const string RegistrationSchema = "CriterionApplicability.Registration.v1";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _editionId;
    readonly string _criterionIdentifier;
    readonly List<object> _facts = [];
    readonly List<VersionState> _versions = [];
    CriterionApplicabilityView? _view;
    long _revision;

    /// <summary>Opens the applicability line of one criterion, for transient business validation and explicit history composition.</summary>
    public CriterionApplicabilityRules(Uuid tenantId, Uuid programId, Uuid editionId, string criterionIdentifier)
    {
        (_tenantId, _programId, _editionId, _criterionIdentifier) =
            (tenantId, programId, editionId, criterionIdentifier);
    }

    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _programId;
    public Uuid EditionId => _editionId;
    public string CriterionIdentifier => _criterionIdentifier;
    public Uuid Id => IdentityOf(_tenantId, _programId, _editionId, _criterionIdentifier);
    public EventStreamAddress Stream => new(_tenantId.ToString(), Area, Id.ToString());

    /// <summary>The logical revision: the number of facts on this line.</summary>
    public long Revision => _revision;

    /// <summary>Every fact of the line in order.</summary>
    public IReadOnlyList<object> Changes => _facts;

    /// <summary>The line's public view, or null before the first proposal.</summary>
    public CriterionApplicabilityView? View => _view;

    /// <summary>The identity of the applicability line of one criterion.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid editionId, string criterionIdentifier) =>
        AggregateIdentity.Create("CriterionApplicability", tenantId, programId, editionId, criterionIdentifier);

    /// <summary>Whether a proposal with this id is on the line.</summary>
    public bool Contains(Uuid decisionId) => _versions.Exists(version => version.DecisionId == decisionId);

    /// <summary>Checks the proposal guards without raising anything.</summary>
    public Result PrepareProposal(long expectedRevision, string rationale)
    {
        if (!IsBoundedText(rationale))
            return Fail(CommandFailure.InvalidContent(
                "A not-applicable decision requires a criterion and a rationale of at most 4000 characters."));
        if (Pending is not null && expectedRevision == _revision)
            return Fail(CommandFailure.StateConflict(
                "The criterion already has a pending not-applicable proposal awaiting review."));
        if (expectedRevision != _revision)
            return Stale();
        return Active is not null
            ? Fail(CommandFailure.StateConflict(
                "The criterion is already not applicable. Withdraw the decision before proposing another."))
            : Result.Success;
    }

    /// <summary>Proposes a not-applicable decision as the root native fact of its operation.</summary>
    public Result Propose(OperationProvenance operation, string rationale, Uuid proposerMemberId,
        ActorReference proposer, DateTimeOffset proposedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(proposer);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A proposal is a root operation of its own applicability line with an expected revision."));
        var prepared = PrepareProposal((long)expected, rationale);
        if (!prepared.IsSuccess)
            return prepared;
        Apply(new CriterionNotApplicableProposedChange(_tenantId, _programId, operation.LogicalId, _revision + 1,
            _versions.Count + 1, _editionId, _criterionIdentifier, rationale.Trim(), proposerMemberId, proposer,
            proposedAt, operation));
        return Result.Success;
    }

    /// <summary>
    ///     Checks the review guards without raising anything. The proposer may review only under an approved waiver
    ///     scoped to (criterion_applicability, decision_id, decision_id, expected_revision, review).
    /// </summary>
    public Result PrepareReview(Uuid decisionId, long expectedRevision, string outcome, string rationale,
        Uuid reviewerMemberId, DateTimeOffset reviewedAt, SeparationOfDutiesWaiver? waiver)
    {
        if (Pending is not { } pending)
            return Fail(CommandFailure.StateConflict(
                "The criterion has no pending not-applicable proposal to review."));
        if (pending.DecisionId != decisionId)
            return Fail(CommandFailure.StateConflict(
                "The decision is not the pending proposal of its criterion."));
        if (expectedRevision != _revision)
            return Stale();
        if (waiver is not null && (waiver.TenantId != _tenantId || !waiver.Allows(
                new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.CriterionApplicability, decisionId,
                    decisionId, expectedRevision, SeparationOfDutiesActions.Review), reviewerMemberId, reviewedAt)))
            return Fail(CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this member and decision revision."));
        if (pending.ProposerMemberId == reviewerMemberId && waiver is null)
            return Fail(CommandFailure.ActorProhibited("A not-applicable proposer cannot review their own proposal."));
        if (outcome is not ("accept" or "reject") || !IsBoundedText(rationale))
            return Fail(CommandFailure.InvalidContent(
                "An applicability review requires an outcome of accept or reject and a rationale of at most 4000 characters."));
        return Result.Success;
    }

    /// <summary>Reviews the pending proposal as the root native fact of its operation.</summary>
    public Result Review(OperationProvenance operation, Uuid decisionId, string outcome, string rationale,
        Uuid reviewerMemberId, ActorReference reviewer, DateTimeOffset reviewedAt, SeparationOfDutiesWaiver? waiver)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(reviewer);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A review is a root operation of its own applicability line with an expected revision."));
        var prepared = PrepareReview(decisionId, (long)expected, outcome, rationale, reviewerMemberId, reviewedAt,
            waiver);
        if (!prepared.IsSuccess)
            return prepared;
        Apply(new CriterionApplicabilityReviewedChange(_tenantId, _programId, decisionId, _revision + 1,
            Pending!.Number, operation.LogicalId, outcome, rationale.Trim(), reviewer, reviewedAt, waiver?.Id,
            _editionId, _criterionIdentifier, operation));
        return Result.Success;
    }

    /// <summary>Checks the withdrawal guards without raising anything.</summary>
    public Result PrepareWithdrawal(Uuid decisionId, long expectedRevision, string rationale)
    {
        if (_versions.Count == 0)
            return Fail(CommandFailure.MissingRecord("The criterion applicability decision was not found."));
        if (expectedRevision != _revision)
            return Stale();
        if (Active is not { } active || Pending is not null)
            return Fail(CommandFailure.StateConflict(
                "Only an accepted not-applicable decision without a pending proposal can be withdrawn."));
        if (active.DecisionId != decisionId)
            return Fail(CommandFailure.StateConflict(
                "The decision is not the accepted decision of its criterion; a newer decision cannot be withdrawn by an older one."));
        if (!IsBoundedText(rationale))
            return Fail(CommandFailure.InvalidContent(
                "Withdrawing a not-applicable decision requires a rationale of at most 4000 characters."));
        return Result.Success;
    }

    /// <summary>Withdraws the accepted decision as the root native fact of its operation.</summary>
    public Result Withdraw(OperationProvenance operation, Uuid decisionId, string rationale, ActorReference actor,
        DateTimeOffset withdrawnAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(actor);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A withdrawal is a root operation of its own applicability line with an expected revision."));
        var prepared = PrepareWithdrawal(decisionId, (long)expected, rationale);
        if (!prepared.IsSuccess)
            return prepared;
        Apply(new CriterionNotApplicableWithdrawnChange(_tenantId, _programId, decisionId, _revision + 1,
            Active!.Number, rationale.Trim(), actor, withdrawnAt, _editionId, _criterionIdentifier, operation));
        return Result.Success;
    }

    VersionState? Pending => _versions.Find(static version => version.Status == "proposed");

    VersionState? Active => _versions.Find(static version => version.Status == "accepted");

    public void Apply(CriterionNotApplicableProposedChange ev)
    {
        if (!Owns(ev.TenantId, ev.ProgramId, ev.EditionId, ev.CriterionIdentifier) || ev.Revision != _revision + 1 ||
            ev.VersionNumber != _versions.Count + 1 || Pending is not null || ev.Operation.Owner != Stream ||
            ev.Operation.LogicalId != ev.DecisionId || Contains(ev.DecisionId))
            throw new InvalidOperationException(
                "A not-applicable proposal follows the previous revision of its own line while nothing is pending.");
        _versions.Add(new VersionState(ev.VersionNumber, ev.DecisionId, ev.ProposerMemberId));
        Fold(ev);
    }

    public void Apply(CriterionApplicabilityReviewedChange ev)
    {
        if (!Owns(ev.TenantId, ev.ProgramId, ev.EditionId, ev.CriterionIdentifier) || ev.Revision != _revision + 1 ||
            Pending is not { } pending || pending.DecisionId != ev.DecisionId || pending.Number != ev.VersionNumber ||
            ev.Operation.Owner != Stream)
            throw new InvalidOperationException(
                "An applicability review follows the previous revision of its own line and names the pending proposal.");
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

    public void Apply(CriterionNotApplicableWithdrawnChange ev)
    {
        if (!Owns(ev.TenantId, ev.ProgramId, ev.EditionId, ev.CriterionIdentifier) || ev.Revision != _revision + 1 ||
            Pending is not null || Active is not { } active || active.DecisionId != ev.DecisionId ||
            active.Number != ev.VersionNumber || ev.Operation.Owner != Stream)
            throw new InvalidOperationException(
                "An applicability withdrawal follows the previous revision of its own line and names the accepted decision.");
        active.Status = "withdrawn";
        Fold(ev);
    }

    public void Apply(object change)
    {
        switch (change)
        {
            case CriterionNotApplicableProposedChange value:
                Apply(value);
                break;
            case CriterionApplicabilityReviewedChange value:
                Apply(value);
                break;
            case CriterionNotApplicableWithdrawnChange value:
                Apply(value);
                break;
            default:
                throw new InvalidOperationException("The immutable change has another mapping family.");
        }
    }

    void Fold(object ev)
    {
        _view = CriterionApplicabilityReducer.Apply(_view, ev);
        _revision = _view.Revision;
        _facts.Add(ev);
    }

    bool Owns(Uuid tenantId, Uuid programId, Uuid editionId, string criterionIdentifier) =>
        tenantId == _tenantId && programId == _programId && editionId == _editionId &&
        string.Equals(criterionIdentifier, _criterionIdentifier, StringComparison.Ordinal);

    static bool IsBoundedText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= MaximumTextLength;

    static Result Fail(CommandFailure failure) => Result.Failure(failure.ToRequestError());

    Result Stale() => Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
        "criterion applicability decision", _revision)));

    sealed class VersionState(int number, Uuid decisionId, Uuid proposerMemberId)
    {
        public int Number { get; } = number;
        public Uuid DecisionId { get; } = decisionId;
        public Uuid ProposerMemberId { get; } = proposerMemberId;
        public string Status { get; set; } = "proposed";
    }
}
