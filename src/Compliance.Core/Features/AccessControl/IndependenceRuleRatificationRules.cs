using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Pure transient personal ratification validation; current authority stays on the native coordinator.</summary>
public sealed class IndependenceRuleRatificationRules
{
    readonly List<IndependenceRuleRatificationView> _ratifications = [];
    readonly Dictionary<Uuid, (string Intent, ActorReference Actor, IndependenceRuleRatificationView Response)> _decisions = [];
    readonly List<object> _pending = [];
    readonly List<object> _applied = [];
    public long Sequence { get; private set; }
    public bool HasActiveUse { get; private set; }
    public IReadOnlyList<IndependenceRuleRatificationView> Ratifications => _ratifications.AsReadOnly();
    public IndependenceRuleRatificationView? Active => _ratifications.LastOrDefault();
    public IndependenceRuleRatificationView? ForVersion(long version) => _ratifications.SingleOrDefault(item => item.RuleVersion == version);
    public IReadOnlyList<object> PendingChanges => _pending;
    public IReadOnlyList<object> AppliedChanges => _applied;
    public void ClearPendingChanges() => _pending.Clear();
    public static IndependenceRuleRatificationRules FromActive(long sequence, IndependenceRuleRatificationView? active, bool hasActiveUse = false)
    {
        var rules = new IndependenceRuleRatificationRules { Sequence = sequence, HasActiveUse = hasActiveUse };
        if (active is not null)
            rules._ratifications.Add(active);
        return rules;
    }
    public void Apply(object change) { Apply((IndependenceRatificationRecordedContent)change); _applied.Add(change); }
    void Raise(IndependenceRatificationRecordedContent change) { Apply((object)change); _pending.Add(change); }
    public Result<IndependenceRuleRatificationView> Ratify(Uuid requestId, Uuid ratificationId,
        long ruleVersion, long observedRuleCatalogSequence, long expectedRatificationSequence,
        string ruleContentDigest, string sourceReference, FirmStaffMemberView currentStaff,
        FirmProfessionalDutyDesignationView duty, ActorReference actor, DateTimeOffset recordedAt, bool hasActiveUse = false)
    {
        if (!IndependenceRecordValidation.ValidAttribution(requestId, actor, recordedAt, "firm_staff") ||
            ratificationId == Uuid.Empty || ruleVersion <= 0 || observedRuleCatalogSequence <= 0 ||
            !ValidDigest(ruleContentDigest) || !Bounded(sourceReference, 2000) ||
            currentStaff is not { IsActive: true, Revision: > 0 } ||
            duty is not { IsActive: true, Duty: FirmProfessionalDuty.RuleRatifier, TenantId: null, Revision: > 0 } ||
            duty.UserId != currentStaff.UserId || duty.StaffMemberId != currentStaff.StaffMemberId ||
            duty.DirectoryStaffRevision != currentStaff.Revision || actor.Id != currentStaff.UserId.ToString())
            return Failure(RequestErrorKind.Validation,
                "Rule ratification requires an active, explicitly designated professional and exact immutable draft digest.");

        var request = new RatifyIndependenceRuleVersion(requestId, ratificationId, ruleVersion,
            observedRuleCatalogSequence, expectedRatificationSequence, ruleContentDigest, sourceReference, expectedRatificationSequence);
        if (Retry(requestId, request, actor) is { } retry)
            return retry;
        if (hasActiveUse || HasActiveUse)
            return Result<IndependenceRuleRatificationView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "An engagement personal decision holds the ratification catalog; retry after it settles.", true));
        if (expectedRatificationSequence != Sequence || ruleVersion <= (_ratifications.LastOrDefault()?.RuleVersion ?? 0) ||
            _ratifications.Any(item => item.RatificationId == ratificationId || item.RuleVersion == ruleVersion))
            return Failure(RequestErrorKind.Conflict,
                "Reload the ratification sequence; ratifications are append-only and rule versions advance monotonically.");

        var ratification = new IndependenceRuleRatificationView(ratificationId, ruleVersion,
            observedRuleCatalogSequence, ruleContentDigest, sourceReference, currentStaff.StaffMemberId,
            currentStaff.UserId, currentStaff.Revision, duty.DesignationId, duty.Revision, actor, recordedAt);
        Raise(new IndependenceRatificationRecordedContent(requestId, expectedRatificationSequence, ratification));
        return Result<IndependenceRuleRatificationView>.Success(ratification);
    }

    void Apply(IndependenceRatificationRecordedContent ev)
    {
        var item = ev.Ratification;
        if (ev.ExpectedSequence != Sequence || item.RatificationId == Uuid.Empty || item.RuleVersion <= 0 ||
            item.ObservedRuleCatalogSequence <= 0 || !ValidDigest(item.RuleContentDigest) ||
            !Bounded(item.SourceReference, 2000) || item.StaffMemberId == Uuid.Empty || item.UserId == Uuid.Empty ||
            item.DirectoryStaffRevision <= 0 || item.DutyDesignationId == Uuid.Empty || item.DutyRevision <= 0 ||
            !IndependenceRecordValidation.ValidAttribution(ev.RequestId, item.Actor, item.RecordedAt, "firm_staff") ||
            item.Actor.Id != item.UserId.ToString() || item.RuleVersion <= (_ratifications.LastOrDefault()?.RuleVersion ?? 0) ||
            _ratifications.Any(existing => existing.RatificationId == item.RatificationId ||
                existing.RuleVersion == item.RuleVersion))
            throw new InvalidOperationException("A personal rule ratification must retain its exact source and advance the sequence.");

        var request = new RatifyIndependenceRuleVersion(ev.RequestId, item.RatificationId, item.RuleVersion,
            item.ObservedRuleCatalogSequence, ev.ExpectedSequence, item.RuleContentDigest, item.SourceReference, ev.ExpectedSequence);
        _ratifications.Add(item);
        _decisions.Add(ev.RequestId, (Intent(request), item.Actor, item));
        Sequence++;
    }

    /// <summary>Resolves a complete original authored ratification without reselecting the mutable draft.</summary>
    public Result<IndependenceRuleRatificationView>? Replay(RatifyIndependenceRuleVersion request, ActorReference actor) =>
        Retry(request.CommandId, request, actor);

    Result<IndependenceRuleRatificationView>? Retry(Uuid requestId, object request, ActorReference actor) =>
        _decisions.TryGetValue(requestId, out var previous)
            ? previous.Intent == Intent(request) && previous.Actor.Kind == actor.Kind && previous.Actor.Id == actor.Id
                ? Result<IndependenceRuleRatificationView>.Success(previous.Response)
                : Failure(RequestErrorKind.Conflict,
                    "The request identity already retains another ratification intent or professional actor.")
            : null;

    static bool ValidDigest(string value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    static bool Bounded(string value, int maximum) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximum && !value.Any(char.IsControl) && value == value.Trim();
    static string Intent<T>(T value) => JsonSerializer.Serialize(value, value!.GetType(), ComplianceCoreJsonContext.Default);
    static Result<IndependenceRuleRatificationView> Failure(RequestErrorKind kind, string message) =>
        Result<IndependenceRuleRatificationView>.Failure(new RequestError(kind, message));
}
