using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Current catalog business authority and selected native acceptance; immutable history is read composition.</summary>
public sealed class IndependenceRuleRatificationCatalog : Aggregate, ISourceUseOwner
{
    public const string Area = "independence-rule-ratifications";
    static readonly Uuid CatalogId = Uuid.Parse("b6330afc-1c97-4500-9c6d-c77006fc0dc6", CultureInfo.InvariantCulture);
    public static Uuid Identity => CatalogId;
    public IndependenceRuleRatificationCatalog(Uuid selectedLogicalId = default) : base(CatalogId, new EventStreamAddress(IndependenceCatalogContent.Realm, Area, CatalogId.ToString()))
    {
        Acceptance = new NativeAcceptance(selectedLogicalId);
        Uses = new SourceUseReservations(Stream);
        On<IndependenceRuleRatificationRecorded>(Apply);
        On<IndependenceRuleRatificationCatalogEngagementUseAuthorized>(granted =>
        {
            if (Header is null || granted.Revision != Sequence)
                throw new InvalidOperationException("A catalog use is granted on the exact held business sequence.");
            Uses.ApplyGranted(SourceUsePurpose.EngagementPersonalDecision, granted);
            NativeSequence++;
            Acceptance.Observe(granted);
        });
        On<IndependenceRuleRatificationCatalogEngagementUseSettled>(settled =>
        {
            Uses.ApplySettled(SourceUsePurpose.EngagementPersonalDecision, settled);
            NativeSequence++;
            Acceptance.Observe(settled);
        });
    }
    public NativeAcceptance Acceptance { get; }
    public SourceUseReservations Uses { get; }
    public long NativeSequence { get; private set; }
    public long Sequence => Header?.Sequence ?? 0;
    public IndependenceRuleRatificationHeader? Header { get; private set; }
    public ImmutableContentRef? CurrentContent { get; private set; }
    public IndependenceRuleRatificationRecorded? LatestRatification { get; private set; }

    public Result PrepareRatification(long expectedSequence, long ruleVersion) => Uses.HasActiveUse
        ? Failure("An engagement personal decision holds the ratification catalog; retry after it settles.", true)
        : expectedSequence == Sequence && ruleVersion > (Header?.RuleVersion ?? 0) ? Result.Success
        : Failure("Reload the ratification sequence; versions advance monotonically.");

    public Result Append(IndependenceRuleRatificationRecorded fact, IndependenceRatificationContent prepared, IReadOnlyList<IndependenceRatificationHistoryRead> history, long nativeSequence, ulong physicalPosition)
    {
        if (NativeSequence != nativeSequence || CommittedStreamPosition != physicalPosition)
            return Failure("The catalog native/physical cut changed during immutable preparation.", true);
        if (Uses.HasActiveUse)
            return Failure("An engagement personal decision holds this catalog; retry after it settles.", true);
        try
        {
            if (prepared is null || prepared.Ratification is null || fact.Operation is null || prepared.Ratification.RuleVersion <= (Header?.RuleVersion ?? 0) ||
                prepared.Ratification.RatificationId == Header?.RatificationId || prepared.Previous != CurrentContent || prepared.Sequence != Sequence + 1 || prepared.RequestId != fact.Operation.LogicalId ||
                !IndependenceCatalogContent.ValidRatification(prepared) || fact.Operation.AuthoredIntentSha256 != IndependenceCatalogContent.Intent(prepared) || !IndependenceCatalogContent.Matches(fact.RatificationContent, prepared, IndependenceCatalogContent.RatificationSchema) ||
                fact.Header != IndependenceCatalogContent.Header(prepared))
                return Failure("The catalog mutation differs from its exact immutable business authority.");
            if (!IndependenceCatalogHistory.ProvesAbsent(CurrentContent, Sequence, history, prepared.Ratification.RatificationId))
                return Failure("The immutable history already owns this ratification identity.");
            Validate(fact);
            if (Acceptance.SelectedLogicalId != Uuid.Empty &&
                (Acceptance.SelectedLogicalId != fact.Operation.LogicalId || Acceptance.SelectedFact is not null))
                return Failure("The append does not select one new catalog logical acceptance.");
            RaiseEvent(fact);
            return Result.Success;
        }
        catch (ImmutableContentIntegrityException) { return Failure("The catalog immutable history proof cannot be verified."); }
        catch (InvalidOperationException) { return Failure("The catalog native mutation has another owner or sequence."); }
    }
    void Apply(IndependenceRuleRatificationRecorded fact)
    {
        Validate(fact);
        Header = fact.Header;
        CurrentContent = fact.RatificationContent;
        LatestRatification = fact;
        NativeSequence++;
        Acceptance.Observe(fact);
    }
    void Validate(IndependenceRuleRatificationRecorded fact)
    {
        if (fact.Header is not { } header || fact.Operation is not { } operation ||
            header.Sequence != Sequence + 1 || header.RuleVersion <= (Header?.RuleVersion ?? 0) || header.RatificationId == Uuid.Empty || header.RatificationId == Header?.RatificationId ||
            fact.NativeSequence != NativeSequence + 1 || operation.LogicalId == Uuid.Empty || operation.RootOperationId != operation.LogicalId ||
            operation.CommandKind != nameof(RatifyIndependenceRuleVersion) || operation.Owner != Stream || operation.FirstOwner != Stream || operation.AffectedOwner != Stream ||
            operation.ExpectedRevisionOwner != Stream || operation.Episode != Id.ToString() || operation.OriginalExpectedRevision != unchecked((ulong)Sequence) ||
            !IndependenceCatalogContent.Digest(operation.AuthoredIntentSha256) || operation.Actor != header.Actor || operation.AcceptedAt != header.RecordedAt ||
            !IndependenceRecordValidation.ValidAttribution(operation.LogicalId, header.Actor, header.RecordedAt, "firm_staff") ||
            !IndependenceCatalogContent.Digest(header.RuleContentDigest) ||
            !IndependenceCatalogContent.Reference(fact.RatificationContent, IndependenceCatalogContent.RatificationSchema) ||
            !IndependenceCatalogContent.Reference(fact.FirstResponse, FirstResponses.Schema) ||
            !IndependenceCatalogContent.Reference(fact.OperationResult, "mutation-result-content.v1") || DomainEventPayload.Size(fact) > DomainEventPayload.MaximumBytes)
            throw new InvalidOperationException("The catalog native fact has another exact actor, owner, intent, gate or sequence.");
    }
    /// <summary>Holds the exact current ratifications for an engagement personal decision; the sequence does not advance.</summary>
    public Result AuthorizeEngagementUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (Header is null || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a ratified catalog can be held, on its own stream.", true));
        RaiseEvent(new IndependenceRuleRatificationCatalogEngagementUseAuthorized(Stream, Sequence, generation, intent,
            null, operation, null, null));
        return Result.Success;
    }

    /// <summary>Settles an engagement use, or fences its key when no grant exists yet.</summary>
    public Result SettleEngagementUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement is recorded on the held catalog stream."));
        RaiseEvent(new IndependenceRuleRatificationCatalogEngagementUseSettled(Stream, Sequence, generation, intent,
            null, operation, null, null));
        return Result.Success;
    }


    static Result Failure(string message, bool transient = false) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
