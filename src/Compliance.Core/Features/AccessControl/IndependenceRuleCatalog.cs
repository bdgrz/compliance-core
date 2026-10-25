using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Current catalog business authority and selected native acceptance; immutable history is read composition.</summary>
public sealed class IndependenceRuleCatalog : Aggregate, ISourceUseOwner
{
    public const string Area = "independence-rule-catalog";
    static readonly Uuid CatalogId = Uuid.Parse("f53d539b-6680-510b-9cfb-127b311fa66c", CultureInfo.InvariantCulture);
    public static Uuid Identity => CatalogId;
    public IndependenceRuleCatalog(Uuid selectedLogicalId = default) : base(CatalogId, new EventStreamAddress(IndependenceCatalogContent.Realm, Area, CatalogId.ToString()))
    {
        Acceptance = new NativeAcceptance(selectedLogicalId);
        Uses = new SourceUseReservations(Stream);
        On<IndependenceRulesRevised>(Apply);
        On<IndependenceRuleCatalogEngagementUseAuthorized>(granted =>
        {
            if (Header is null || granted.Revision != Sequence)
                throw new InvalidOperationException("A catalog use is granted on the exact held business sequence.");
            Uses.ApplyGranted(SourceUsePurpose.EngagementPersonalDecision, granted);
            NativeSequence++;
            Acceptance.Observe(granted);
        });
        On<IndependenceRuleCatalogEngagementUseSettled>(settled =>
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
    public IndependenceRuleCatalogHeader? Header { get; private set; }
    public ImmutableContentRef? CurrentContent { get; private set; }
    public IndependenceRulesRevised? LatestRevision { get; private set; }

    public Result PrepareRevision(long expectedSequence) => Uses.HasActiveUse
        ? Failure("An engagement personal decision holds the rule catalog; retry after it settles.", true)
        : expectedSequence == Sequence ? Result.Success : Failure("Reload the independence sequence before revising the draft rules.");

    public Result Append(IndependenceRulesRevised fact, IndependenceRuleVersionContent prepared, long nativeSequence, ulong physicalPosition)
    {
        if (NativeSequence != nativeSequence || CommittedStreamPosition != physicalPosition)
            return Failure("The catalog native/physical cut changed during immutable preparation.", true);
        if (Uses.HasActiveUse)
            return Failure("An engagement personal decision holds this catalog; retry after it settles.", true);
        try
        {
            if (prepared is null || fact.Operation is null || prepared.Previous != CurrentContent || prepared.Sequence != Sequence + 1 || prepared.RequestId != fact.Operation.LogicalId ||
                !IndependenceCatalogContent.ValidRule(prepared) || fact.Operation.AuthoredIntentSha256 != IndependenceCatalogContent.Intent(prepared) || !IndependenceCatalogContent.Matches(fact.RuleContent, prepared, IndependenceCatalogContent.RuleSchema) ||
                fact.Header != IndependenceCatalogContent.Header(prepared))
                return Failure("The catalog mutation differs from its exact immutable business authority.");
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
    void Apply(IndependenceRulesRevised fact)
    {
        Validate(fact);
        Header = fact.Header;
        CurrentContent = fact.RuleContent;
        LatestRevision = fact;
        NativeSequence++;
        Acceptance.Observe(fact);
    }
    void Validate(IndependenceRulesRevised fact)
    {
        if (fact.Header is not { } header || fact.Operation is not { } operation ||
            header.Sequence != Sequence + 1 || header.RuleVersion != header.Sequence ||
            fact.NativeSequence != NativeSequence + 1 || operation.LogicalId == Uuid.Empty || operation.RootOperationId != operation.LogicalId ||
            operation.CommandKind != nameof(ReviseIndependenceRules) || operation.Owner != Stream || operation.FirstOwner != Stream || operation.AffectedOwner != Stream ||
            operation.ExpectedRevisionOwner != Stream || operation.Episode != Id.ToString() || operation.OriginalExpectedRevision != unchecked((ulong)Sequence) ||
            !IndependenceCatalogContent.Digest(operation.AuthoredIntentSha256) || operation.Actor != header.Actor || operation.AcceptedAt != header.RecordedAt ||
            !IndependenceRecordValidation.ValidAttribution(operation.LogicalId, header.Actor, header.RecordedAt, "platform_operator") ||
            !IndependenceCatalogContent.Digest(header.RuleContentDigest) ||
            !IndependenceCatalogContent.Reference(fact.RuleContent, IndependenceCatalogContent.RuleSchema) ||
            !IndependenceCatalogContent.Reference(fact.FirstResponse, FirstResponses.Schema) ||
            !IndependenceCatalogContent.Reference(fact.OperationResult, "mutation-result-content.v1") || DomainEventPayload.Size(fact) > DomainEventPayload.MaximumBytes)
            throw new InvalidOperationException("The catalog native fact has another exact actor, owner, intent, gate or sequence.");
    }
    /// <summary>Holds the exact current catalog content for an engagement personal decision; the sequence does not advance.</summary>
    public Result AuthorizeEngagementUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (Header is null || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a revised catalog can be held, on its own stream.", true));
        RaiseEvent(new IndependenceRuleCatalogEngagementUseAuthorized(Stream, Sequence, generation, intent, null,
            operation, null, null));
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
        RaiseEvent(new IndependenceRuleCatalogEngagementUseSettled(Stream, Sequence, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }


    static Result Failure(string message, bool transient = false) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
