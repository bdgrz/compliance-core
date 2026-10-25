using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Pure transient rule validation and immutable read composition.</summary>
public sealed class IndependenceRuleCatalogRules
{
    readonly List<IndependenceRuleVersionView> _rules = [];
    readonly Dictionary<Uuid, (string Intent, ActorReference Actor, IndependenceRuleVersionView Response)> _decisions = [];
    readonly List<object> _pending = [];
    readonly List<object> _applied = [];
    public long Sequence { get; private set; }
    public bool HasActiveUse { get; private set; }
    public IReadOnlyList<IndependenceRuleVersionView> Versions => _rules.AsReadOnly();
    public IndependenceRuleVersionView? Current => _rules.LastOrDefault();
    public IReadOnlyList<object> PendingChanges => _pending;
    public IReadOnlyList<object> AppliedChanges => _applied;
    public void ClearPendingChanges() => _pending.Clear();
    public static IndependenceRuleCatalogRules FromCurrent(long sequence, IndependenceRuleVersionView? current, bool hasActiveUse = false)
    {
        var rules = new IndependenceRuleCatalogRules { Sequence = sequence, HasActiveUse = hasActiveUse };
        if (current is not null)
            rules._rules.Add(IndependenceRecordValidation.Freeze(current));
        return rules;
    }
    public void Apply(object change) { Apply((IndependenceRulesRevisionContent)change); _applied.Add(change); }
    void Raise(IndependenceRulesRevisionContent change) { Apply((object)change); _pending.Add(change); }
    public Result<IndependenceRuleVersionView> ReviseRules(Uuid requestId, long expectedSequence,
        IndependenceRuleContent content, ActorReference actor, DateTimeOffset recordedAt, bool hasActiveUse = false)
    {
        if (!IndependenceRecordValidation.ValidAttribution(requestId, actor, recordedAt, "platform_operator") || !IndependenceRecordValidation.ValidRules(content))
            return Failure<IndependenceRuleVersionView>(RequestErrorKind.Validation,
                "Draft rules require bounded classified services, a look-back of at least twelve months and attributed source facts.");
        content = content with { ServiceRules = Array.AsReadOnly(content.ServiceRules.ToArray()) };
        var request = new ReviseIndependenceRules(requestId, expectedSequence, content);
        if (Retry<IndependenceRuleVersionView>(requestId, request, actor) is { } retry)
            return retry;
        if (hasActiveUse || HasActiveUse)
            return Result<IndependenceRuleVersionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "An engagement personal decision holds the rule catalog; retry after it settles.", true));
        if (expectedSequence != Sequence)
            return Failure<IndependenceRuleVersionView>(RequestErrorKind.Conflict,
                "Reload the independence sequence before revising the draft rules.");
        var version = new IndependenceRuleVersionView(Sequence + 1, content, actor, recordedAt, false);
        var revised = new IndependenceRulesRevisionContent(requestId, expectedSequence, version);
        Raise(revised);
        return Result<IndependenceRuleVersionView>.Success(version);
    }

    void Apply(IndependenceRulesRevisionContent revised)
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
        Sequence++;
        _decisions.Add(revised.RequestId, (Intent(new ReviseIndependenceRules(revised.RequestId, revised.ExpectedSequence,
            version.Content)), version.Actor, version));
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
    static Result<T> Failure<T>(RequestErrorKind kind, string message) =>
        Result<T>.Failure(new RequestError(kind, message));
}
