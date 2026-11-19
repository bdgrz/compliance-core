using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Aggregates;

/// <summary>Owns the immutable identity of one entry operation across all entry command kinds.</summary>
/// <remarks>
/// This owner stores one claim event and never stores a collection of attempts.
/// It does not record a business outcome or prove source freshness. All entry
/// command paths must claim this owner before deciding the target. The caller
/// supplies trusted server-produced admission and must still enforce the exact
/// target's execution generation and source-position fences.
/// </remarks>
public sealed class CriteriaEntryOperation : Aggregate
{
    readonly Uuid _editionId;
    readonly string _identifier;
    readonly Uuid _operationId;
    CriterionEntryOperationClaimed? _claim;
    CriterionEntryOperationSeedClaimed? _seedClaim;

    /// <summary>Opens the stable operation identity for one entry.</summary>
    public CriteriaEntryOperation(Uuid editionId, string identifier, Uuid operationId)
        : base(IdentityOf(editionId, identifier, operationId), AddressOf(editionId, identifier, operationId))
    {
        _editionId = editionId;
        _identifier = identifier;
        _operationId = operationId;
        On<CriterionEntryOperationClaimed>(Apply);
        On<CriterionEntryOperationSeedClaimed>(Apply);
    }

    /// <summary>Creates the deterministic owner identity.</summary>
    public static Uuid IdentityOf(Uuid editionId, string identifier, Uuid operationId) =>
        AggregateIdentity.Create("CriteriaEntryOperation", editionId, identifier, operationId);

    /// <summary>Places the independent operation in the edition's ordered source area.</summary>
    public static EventStreamAddress AddressOf(Uuid editionId, string identifier, Uuid operationId) =>
        new("bdgrz", $"criteria-{editionId}", $"entry-operation-{IdentityOf(editionId, identifier, operationId)}");

    /// <summary>The original immutable claim, or null before the first accepted write.</summary>
    public CriterionEntryOperationClaimed? ClaimFact => _claim;

    /// <summary>The original trusted seed claim, or null for a human operation.</summary>
    public CriterionEntryOperationSeedClaimed? SeedClaimFact => _seedClaim;

    /// <summary>Claims this operation identity for a structural intent.</summary>
    public Result Claim(CriterionChangeIntent intent, long executionGeneration, ActorAttribution actor,
        AccessAdmissionEvidence admission)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!CriteriaContentBounds.StructuralShape(intent))
            return Failure(RequestErrorKind.Validation, "A bounded structural intent and draft identity are required.");
        return ClaimCore(intent, null, executionGeneration, actor, admission);
    }

    /// <summary>Claims this operation identity for an exact summary intent.</summary>
    public Result Claim(CriterionSummaryIntent intent, long executionGeneration, ActorAttribution actor,
        AccessAdmissionEvidence admission)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!CriteriaContentBounds.SummaryIntent(intent) || intent.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation, "A bounded summary intent and nonnegative revision are required.");
        return ClaimCore(null, intent, executionGeneration, actor, admission);
    }

    Result ClaimCore(CriterionChangeIntent? structural, CriterionSummaryIntent? summary, long executionGeneration,
        ActorAttribution actor, AccessAdmissionEvidence admission)
    {
        if (!AccessAdmissionEvidenceBinding.IsCurrentFormat(admission) || !Valid(structural, summary, executionGeneration, actor, admission))
            return Failure(RequestErrorKind.Validation, "The exact operation, actor, target and trusted admission are required.");
        if (_seedClaim is not null)
            return Failure(RequestErrorKind.Conflict, "This entry operation identity belongs to the configured seed.");
        if (_claim is { } previous)
            return previous.Structural == structural && previous.Summary == summary && previous.Actor == actor &&
                previous.Admission.UserId == admission.UserId
                ? Result.Success
                : Failure(RequestErrorKind.Conflict, "This entry operation identity already has different intent or actor.");

        var fact = new CriterionEntryOperationClaimed(structural, summary, executionGeneration, actor, admission);
        if (!CriteriaContentBounds.Payload(fact))
            return Failure(RequestErrorKind.Validation, "The operation claim exceeds the criteria event payload limit.");
        RaiseEvent(fact);
        return Result.Success;
    }

    /// <summary>Claims the identity of one fixed seed entry under trusted bootstrap authority.</summary>
    /// <remarks>The caller must verify the exact committed edition binding and parent evidence from source.</remarks>
    internal Result ClaimSeed(CriterionChangeIntent intent, long executionGeneration, Uuid incarnationId,
        Uuid? parentAcceptedEventId, CriteriaEditionDraftRequested binding)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(binding);
        var expected = PlatformCriteriaCatalog.SeedEdition();
        if (binding.Metadata.EventId == Uuid.Empty || binding.Metadata.AggregateId != _editionId ||
            binding.Metadata.IsAudit || binding.EditionId != _editionId || binding.Framework != expected.Framework ||
            binding.DraftGeneration != intent.DraftGeneration || binding.OperationId != intent.DraftGeneration ||
            binding.OperationId != PlatformCriteriaSeedDefinition.OperationOf(_editionId, "edition") ||
            binding.ExecutionGeneration <= 0 || binding.Label != expected.EditionLabel || binding.Admission is not null ||
            binding.Actor != new ActorAttribution("criteria-seed-bootstrap", "system") ||
            !PlatformCriteriaSeedDefinition.MatchesBinding(_editionId, binding.SeedVersion, binding.SeedFingerprint))
            return Failure(RequestErrorKind.Validation, "The exact configured seed edition binding is required.");
        var proposal = new CriterionSeedProposed(intent, executionGeneration, incarnationId, parentAcceptedEventId,
            binding.Metadata.EventId, Uuid.Empty, binding.SeedVersion!, binding.SeedFingerprint!);
        if (!ValidSeed(proposal))
            return Failure(RequestErrorKind.Validation, "The exact configured seed intent and operation identity are required.");
        if (_claim is not null)
            return Failure(RequestErrorKind.Conflict, "This entry operation identity already belongs to a human intent.");
        if (_seedClaim is { } previous)
            return previous.Intent == proposal.Intent && previous.IncarnationId == proposal.IncarnationId &&
                previous.ParentAcceptedEventId == proposal.ParentAcceptedEventId &&
                previous.EditionRequestEventId == proposal.EditionRequestEventId &&
                previous.SeedVersion == proposal.SeedVersion && previous.SeedFingerprint == proposal.SeedFingerprint
                ? Result.Success : Failure(RequestErrorKind.Conflict, "This seed identity already has different intent.");
        var fact = new CriterionEntryOperationSeedClaimed(proposal.Intent, proposal.ExecutionGeneration,
            proposal.IncarnationId, proposal.ParentAcceptedEventId, proposal.EditionRequestEventId,
            proposal.SeedVersion, proposal.SeedFingerprint);
        if (!CriteriaContentBounds.Payload(fact))
            return Failure(RequestErrorKind.Validation, "The seed operation claim exceeds the criteria event payload limit.");
        RaiseEvent(fact);
        return Result.Success;
    }

    void Apply(CriterionEntryOperationClaimed fact)
    {
        if (_claim is not null || _seedClaim is not null ||
            !Valid(fact.Structural, fact.Summary, fact.ExecutionGeneration, fact.Actor, fact.Admission) ||
            !CriteriaContentBounds.Payload(fact) ||
            (fact.Structural is { } structural && !CriteriaContentBounds.StructuralShape(structural)) ||
            (fact.Summary is { } summary && (!CriteriaContentBounds.SummaryIntent(summary) || summary.ExpectedRevision < 0)))
            throw new InvalidOperationException("An entry operation can be claimed only once by its exact admitted intent.");
        _claim = fact;
    }

    void Apply(CriterionEntryOperationSeedClaimed fact)
    {
        var proposal = new CriterionSeedProposed(fact.Intent, fact.ExecutionGeneration, fact.IncarnationId,
            fact.ParentAcceptedEventId, fact.EditionRequestEventId, Uuid.Empty, fact.SeedVersion, fact.SeedFingerprint);
        if (_claim is not null || _seedClaim is not null || !ValidSeed(proposal) || !CriteriaContentBounds.Payload(fact))
            throw new InvalidOperationException("A seed operation must be claimed once by the exact configured intent.");
        _seedClaim = fact;
    }

    bool ValidSeed(CriterionSeedProposed proposal) => _editionId != Uuid.Empty && _operationId != Uuid.Empty &&
        proposal.Intent is { } intent && intent.EditionId == _editionId && intent.OperationId == _operationId &&
        intent.Identifier == _identifier && PlatformCriteriaSeedDefinition.MatchesProposal(proposal, requireClaim: false);

    bool Valid(CriterionChangeIntent? structural, CriterionSummaryIntent? summary, long generation,
        ActorAttribution? actor, AccessAdmissionEvidence? admission)
    {
        if ((structural is null) == (summary is null) || _editionId == Uuid.Empty || _operationId == Uuid.Empty ||
            !CriteriaContentBounds.Identifier(_identifier) || _identifier != _identifier.Trim() || generation <= 0 ||
            !CriteriaContentBounds.AdmissionFields(actor, admission) || actor is null || admission is null)
            return false;
        var editionId = structural?.EditionId ?? summary!.EditionId;
        var identifier = structural?.Identifier ?? summary!.Identifier;
        var operationId = structural?.OperationId ?? summary!.OperationId;
        if (editionId != _editionId || identifier != _identifier || operationId != _operationId)
            return false;
        var digest = structural is not null ? AuthoredIntentDigest.Compute(structural) : AuthoredIntentDigest.Compute(summary!);
        return AccessAdmissionEvidenceBinding.BindsCriteriaOperator(admission, admission.UserId, actor, operationId,
            CriteriaEntry.StreamFor(_editionId, _identifier).ToString(), CriteriaEdition.BindAdmissionDigest(digest, generation));
    }

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
