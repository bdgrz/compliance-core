using System.Globalization;
using System.Text.Json;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Retains personal rule ratifications separately from platform-authored drafts. Ratifications are append-only
///     and unbounded in number. The catalog embeds <see cref="SourceUseReservations" />: an engagement personal
///     decision that holds the exact ratification does not change it, but a held catalog refuses new ratifications
///     until the use is settled.
/// </summary>
public sealed class IndependenceRuleRatificationCatalog : Aggregate, ISourceUseOwner
{
    /// <summary>Stream area of the ratification catalog.</summary>
    public const string Area = "independence-rule-ratifications";

    static readonly Uuid CatalogId = Uuid.Parse("b6330afc-1c97-4500-9c6d-c77006fc0dc6", CultureInfo.InvariantCulture);
    readonly List<IndependenceRuleRatificationView> _ratifications = [];
    readonly Dictionary<Uuid, (string Intent, ActorReference Actor, IndependenceRuleRatificationView Response)> _decisions = [];

    IndependenceRuleRatificationRecorded? _latest;

    /// <summary>Opens the catalog, selecting the logical operation to resolve.</summary>
    public IndependenceRuleRatificationCatalog(Uuid selectedLogicalId = default) : base(CatalogId,
        new EventStreamAddress("bdgrz", Area, CatalogId.ToString()))
    {
        Acceptance = new NativeAcceptance(selectedLogicalId);
        Uses = new SourceUseReservations(Stream);
        On<IndependenceRuleRatificationRecorded>(Apply);
        On<IndependenceRuleRatificationCatalogEngagementUseAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (_latest is null || granted.Revision != Sequence)
                throw new InvalidOperationException("A catalog use is granted on a ratified catalog at its held sequence.");
            Uses.ApplyGranted(SourceUsePurpose.EngagementPersonalDecision, granted);
        });
        On<IndependenceRuleRatificationCatalogEngagementUseSettled>(settled =>
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

    /// <summary>The latest ratification fact, which a use pins; null while nothing was ratified.</summary>
    public IndependenceRuleRatificationRecorded? LatestRatification => _latest;

    public long Sequence { get; private set; }
    public IReadOnlyList<IndependenceRuleRatificationView> Ratifications =>
        Array.AsReadOnly(_ratifications.ToArray());
    public IndependenceRuleRatificationView? Active => _ratifications.LastOrDefault();

    public IndependenceRuleRatificationView? ForVersion(long ruleVersion) =>
        _ratifications.SingleOrDefault(item => item.RuleVersion == ruleVersion);

    public Result<IndependenceRuleRatificationView> Ratify(Uuid requestId, Uuid ratificationId,
        long ruleVersion, long observedRuleCatalogSequence, long expectedRatificationSequence,
        string ruleContentDigest, string sourceReference, FirmStaffMemberView currentStaff,
        FirmProfessionalDutyDesignationView duty, ActorReference actor, DateTimeOffset recordedAt)
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

        var request = new RatifyIndependenceRuleVersion(ratificationId, ruleVersion,
            observedRuleCatalogSequence, expectedRatificationSequence, ruleContentDigest, sourceReference);
        if (Retry(requestId, request, actor) is { } retry)
            return retry;
        if (Uses.HasActiveUse)
            return Result<IndependenceRuleRatificationView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "An engagement personal decision holds the ratification catalog; retry after it settles.", true));
        if (expectedRatificationSequence != Sequence || ruleVersion <= (_ratifications.LastOrDefault()?.RuleVersion ?? 0) ||
            _ratifications.Any(item => item.RatificationId == ratificationId || item.RuleVersion == ruleVersion))
            return Failure(RequestErrorKind.Conflict,
                "Reload the ratification sequence; ratifications are append-only and rule versions advance monotonically.");

        var ratification = new IndependenceRuleRatificationView(ratificationId, ruleVersion,
            observedRuleCatalogSequence, ruleContentDigest, sourceReference, currentStaff.StaffMemberId,
            currentStaff.UserId, currentStaff.Revision, duty.DesignationId, duty.Revision, actor, recordedAt);
        RaiseEvent(new IndependenceRuleRatificationRecorded(requestId, expectedRatificationSequence, ratification));
        return Result<IndependenceRuleRatificationView>.Success(ratification);
    }

    void Apply(IndependenceRuleRatificationRecorded ev)
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

        var request = new RatifyIndependenceRuleVersion(item.RatificationId, item.RuleVersion,
            item.ObservedRuleCatalogSequence, ev.ExpectedSequence, item.RuleContentDigest, item.SourceReference);
        _ratifications.Add(item);
        _latest = ev;
        _decisions.Add(ev.RequestId, (Intent(request), item.Actor, item));
        Sequence++;
    }

    /// <summary>Holds the exact current ratifications for an engagement personal decision; the sequence does not advance.</summary>
    public Result AuthorizeEngagementUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (_latest is null || operation.Owner != Stream)
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

    Result<IndependenceRuleRatificationView>? Retry(Uuid requestId, object request, ActorReference actor) =>
        _decisions.TryGetValue(requestId, out var previous)
            ? previous.Intent == Intent(request) && previous.Actor == actor
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
