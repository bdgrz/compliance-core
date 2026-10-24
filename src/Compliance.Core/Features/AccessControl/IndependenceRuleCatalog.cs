using System.Globalization;
using System.Text.Json;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Platform content only: attributable draft classifications, never client facts or ratification. Versions are
///     append-only and unbounded in number. The catalog embeds <see cref="SourceUseReservations" />: an engagement
///     personal decision that holds the exact catalog content does not change it, but a held catalog refuses new
///     versions until the use is settled.
/// </summary>
public sealed class IndependenceRuleCatalog : Aggregate, ISourceUseOwner
{
    /// <summary>Stream area of the draft rule catalog.</summary>
    public const string Area = "independence-rule-catalog";

    static readonly Uuid CatalogId = Uuid.Parse("f53d539b-6680-510b-9cfb-127b311fa66c", CultureInfo.InvariantCulture);
    readonly List<IndependenceRuleVersionView> _rules = [];
    readonly Dictionary<Uuid, (string Intent, ActorReference Actor, IndependenceRuleVersionView Response)> _decisions = [];

    IndependenceRulesRevised? _latest;

    /// <summary>Opens the catalog, selecting the logical operation to resolve.</summary>
    public IndependenceRuleCatalog(Uuid selectedLogicalId = default) : base(CatalogId,
        new EventStreamAddress("bdgrz", Area, CatalogId.ToString()))
    {
        Acceptance = new NativeAcceptance(selectedLogicalId);
        Uses = new SourceUseReservations(Stream);
        On<IndependenceRulesRevised>(Apply);
        On<IndependenceRuleCatalogEngagementUseAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (_latest is null || granted.Revision != Sequence)
                throw new InvalidOperationException("A catalog use is granted on a revised catalog at its held sequence.");
            Uses.ApplyGranted(SourceUsePurpose.EngagementPersonalDecision, granted);
        });
        On<IndependenceRuleCatalogEngagementUseSettled>(settled =>
        {
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.EngagementPersonalDecision, settled);
        });
    }

    /// <summary>The stable identity of the catalog, which the use receivers address as their source owner.</summary>
    public static Uuid Identity => CatalogId;

    /// <summary>The active grants, receiver generation and fences of this catalog's source uses.</summary>
    public SourceUseReservations Uses { get; }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The latest revision fact, which a use pins; null while the catalog is empty.</summary>
    public IndependenceRulesRevised? LatestRevision => _latest;

    public long Sequence => _rules.Count;
    public IReadOnlyList<IndependenceRuleVersionView> Versions => Array.AsReadOnly(_rules.ToArray());
    public IndependenceRuleVersionView? Current => _rules.LastOrDefault();

    public Result<IndependenceRuleVersionView> ReviseRules(Uuid requestId, long expectedSequence,
        IndependenceRuleContent content, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!IndependenceRecordValidation.ValidAttribution(requestId, actor, recordedAt, "platform_operator") || !IndependenceRecordValidation.ValidRules(content))
            return Failure<IndependenceRuleVersionView>(RequestErrorKind.Validation,
                "Draft rules require bounded classified services, a look-back of at least twelve months and attributed source facts.");
        content = content with { ServiceRules = Array.AsReadOnly(content.ServiceRules.ToArray()) };
        var request = new ReviseIndependenceRules(expectedSequence, content);
        if (Retry<IndependenceRuleVersionView>(requestId, request, actor) is { } retry)
            return retry;
        if (Uses.HasActiveUse)
            return Result<IndependenceRuleVersionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "An engagement personal decision holds the rule catalog; retry after it settles.", true));
        if (expectedSequence != Sequence)
            return Failure<IndependenceRuleVersionView>(RequestErrorKind.Conflict,
                "Reload the independence sequence before revising the draft rules.");
        var version = new IndependenceRuleVersionView(_rules.Count + 1, content, actor, recordedAt, false);
        var revised = new IndependenceRulesRevised(requestId, expectedSequence, version);
        if (!Fits(revised))
            return Failure<IndependenceRuleVersionView>(RequestErrorKind.Validation, "The rule event exceeds the bounded payload.");
        RaiseEvent(revised);
        return Result<IndependenceRuleVersionView>.Success(version);
    }

    void Apply(IndependenceRulesRevised revised)
    {
        if (revised.ExpectedSequence != Sequence || revised.Version.Version != Sequence + 1 ||
            revised.Version.IsRatified || !IndependenceRecordValidation.ValidRules(revised.Version.Content))
            throw new InvalidOperationException("Draft rule versions must be consecutive, valid and cannot claim ratification.");
        var version = revised.Version with
        {
            Content = revised.Version.Content with
            {
                ServiceRules = Array.AsReadOnly(revised.Version.Content.ServiceRules.ToArray())
            }
        };
        _rules.Add(version);
        _latest = revised;
        _decisions.Add(revised.RequestId, (Intent(new ReviseIndependenceRules(revised.ExpectedSequence,
            version.Content)), version.Actor, version));
    }

    /// <summary>Holds the exact current catalog content for an engagement personal decision; the sequence does not advance.</summary>
    public Result AuthorizeEngagementUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (_latest is null || operation.Owner != Stream)
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

    Result<T>? Retry<T>(Uuid requestId, object request, ActorReference actor)
    {
        if (!_decisions.TryGetValue(requestId, out var previous))
            return null;
        return previous.Intent == Intent(request) && previous.Actor == actor && previous.Response is T response
            ? Result<T>.Success(response)
            : Failure<T>(RequestErrorKind.Conflict, "The request identity already retains a different rule intent or actor.");
    }

    static string Intent<T>(T value) => JsonSerializer.Serialize(value, value!.GetType(), ComplianceCoreJsonContext.Default);
    static bool Fits<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value,
        value!.GetType(), ComplianceCoreJsonContext.Default).Length <= 48 * 1024;
    static Result<T> Failure<T>(RequestErrorKind kind, string message) =>
        Result<T>.Failure(new RequestError(kind, message));
}
