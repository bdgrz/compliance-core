using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Commands;
using Bdgrz.Compliance.Features.FirmServices.Events;
using Bdgrz.Compliance.Features.FirmServices.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
namespace Bdgrz.Compliance.Features.FirmServices.Aggregates;

/// <summary>One numbered draft becomes immutable when ratified.</summary>
public sealed class IndependenceRuleSet : Aggregate
{
    public const string Area = "firm-independence-rule-set";
    static readonly string[] ServiceKinds = ["readiness_assessment", "control_design", "control_implementation", "control_operation", "vciso"];
    readonly long _version;
    readonly Uuid _selectedCommandId;
    readonly HashSet<Uuid> _commands = [];
    FirmServiceReceipt? _selectedReceipt;
    IndependenceRuleSetView? _selectedView;
    public IndependenceRuleSetView? View { get; private set; }
    public long Revision => View?.Revision ?? 0;

    public IndependenceRuleSet(long version, Uuid selectedCommandId = default)
        : base(AggregateIdentity.Create(nameof(IndependenceRuleSet), version.ToString(CultureInfo.InvariantCulture)),
            new EventStreamAddress("platform", Area, version.ToString(CultureInfo.InvariantCulture)))
    {
        _version = version;
        _selectedCommandId = selectedCommandId;
        On<IndependenceRuleSetDrafted>(fact => Apply(fact.Version, fact.Revision, fact.Terms, false, fact.Receipt, true));
        On<IndependenceRuleSetDraftRevised>(fact => Apply(fact.Version, fact.Revision, fact.Terms, false, fact.Receipt, false));
        On<IndependenceRuleSetRatified>(fact => Apply(fact.Version, fact.Revision, View?.Terms!, true, fact.Receipt, false));
    }

    public Result<IndependenceRuleSetView> Draft(DraftIndependenceRuleSet request,
        IndependenceRuleSetView? predecessor, Uuid actorUserId, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(request);
        var digest = AuthoredIntentDigest.Compute(request);
        if (Replay(request.CommandId, digest, actorUserId) is { } retry)
            return retry;
        if (Validate(request.Version, request.CommandId, 0, actorUserId, at) is { } invalid)
            return Result<IndependenceRuleSetView>.Failure(invalid);
        if (_version > 1 && (predecessor is null || !predecessor.IsRatified || predecessor.Version != _version - 1))
            return Error(RequestErrorKind.Conflict, "The immediately preceding rule version must be ratified.");
        if (!ValidTerms(request.Terms))
            return Error(RequestErrorKind.Validation, "Every advisory service kind needs one classification and a valid look-back.");
        RaiseEvent(new IndependenceRuleSetDrafted(_version, 1, Freeze(request.Terms),
            new(request.CommandId, 0, digest, actorUserId, at)));
        return Result<IndependenceRuleSetView>.Success(View!);
    }

    public Result<IndependenceRuleSetView> Revise(ReviseIndependenceRuleSetDraft request,
        Uuid actorUserId, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(request);
        var digest = AuthoredIntentDigest.Compute(request);
        if (Replay(request.CommandId, digest, actorUserId) is { } retry)
            return retry;
        if (Validate(request.Version, request.CommandId, request.ExpectedRevision, actorUserId, at) is { } invalid)
            return Result<IndependenceRuleSetView>.Failure(invalid);
        if (View is null)
            return Error(RequestErrorKind.NotFound, "The rule-set draft was not found.");
        if (View.IsRatified)
            return Error(RequestErrorKind.Conflict, "A ratified rule-set version is immutable.");
        if (!ValidTerms(request.Terms))
            return Error(RequestErrorKind.Validation, "Every advisory service kind needs one classification and a valid look-back.");
        if (SameTerms(View.Terms, request.Terms))
            return Result<IndependenceRuleSetView>.Success(View);
        RaiseEvent(new IndependenceRuleSetDraftRevised(_version, Revision + 1, Freeze(request.Terms),
            new(request.CommandId, request.ExpectedRevision, digest, actorUserId, at)));
        return Result<IndependenceRuleSetView>.Success(View!);
    }

    public Result<IndependenceRuleSetView> Ratify(RatifyIndependenceRuleSet request,
        Uuid actorUserId, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(request);
        var digest = AuthoredIntentDigest.Compute(request);
        if (Replay(request.CommandId, digest, actorUserId) is { } retry)
            return retry;
        if (Validate(request.Version, request.CommandId, request.ExpectedRevision, actorUserId, at) is { } invalid)
            return Result<IndependenceRuleSetView>.Failure(invalid);
        if (View is null)
            return Error(RequestErrorKind.NotFound, "The rule-set draft was not found.");
        if (View.IsRatified)
            return Error(RequestErrorKind.Conflict, "The rule-set version is already ratified.");
        RaiseEvent(new IndependenceRuleSetRatified(_version, Revision + 1,
            new(request.CommandId, request.ExpectedRevision, digest, actorUserId, at)));
        return Result<IndependenceRuleSetView>.Success(View!);
    }

    RequestError? Validate(long version, Uuid commandId, long expectedRevision, Uuid actorUserId, DateTimeOffset at)
    {
        if (_version <= 0 || version != _version || commandId == Uuid.Empty || actorUserId == Uuid.Empty ||
            at == default || View is not null && at < View.RecordedAt)
            return new(RequestErrorKind.Validation, "The exact rule-set version, actor and time are required.");
        return expectedRevision != Revision || _commands.Contains(commandId)
            ? new(RequestErrorKind.Conflict, "Reload the rule-set version before changing it.") : null;
    }

    Result<IndependenceRuleSetView>? Replay(Uuid commandId, string digest, Uuid actorUserId) =>
        _selectedReceipt is not { } receipt || receipt.CommandId != commandId ? null :
        receipt.ActorUserId == actorUserId && receipt.AuthoredIntentSha256 == digest
            ? Result<IndependenceRuleSetView>.Success(_selectedView!)
            : Error(RequestErrorKind.Conflict, "The command already records another actor or intent.");

    void Apply(long version, long revision, IndependenceRuleSetTerms terms, bool ratified,
        FirmServiceReceipt receipt, bool creating)
    {
        if (receipt is null || revision != Revision + 1 ||
            Validate(version, receipt.CommandId, receipt.ExpectedRevision, receipt.ActorUserId, receipt.OccurredAt) is not null ||
            creating != (View is null) || View?.IsRatified == true || !ValidTerms(terms) ||
            !creating && !ratified && SameTerms(View!.Terms, terms) ||
            receipt.AuthoredIntentSha256 is not { Length: 64 } hash ||
            hash.Any(static c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')) ||
            !_commands.Add(receipt.CommandId))
            throw new InvalidOperationException("The independence rule-set history is invalid.");
        View = new(_version, revision, Freeze(terms), ratified, receipt.ActorUserId, receipt.OccurredAt);
        if (_selectedCommandId == Uuid.Empty || _selectedCommandId == receipt.CommandId)
        {
            _selectedReceipt = receipt;
            _selectedView = View;
        }
    }

    public static bool ValidTerms(IndependenceRuleSetTerms? terms) => terms is { LookBackMonths: > 0 and <= 1200 } &&
        terms.Classifications is { Count: 5 } classifications &&
        classifications.All(static value => value is not null && ServiceKinds.Contains(value.ServiceKind, StringComparer.Ordinal) &&
            value.Classification is "impairing" or "conditionally_compatible") &&
        classifications.Select(static value => value.ServiceKind).Distinct(StringComparer.Ordinal).Count() == ServiceKinds.Length;

    static IndependenceRuleSetTerms Freeze(IndependenceRuleSetTerms terms) =>
        new(terms.LookBackMonths, Array.AsReadOnly(terms.Classifications.OrderBy(static value => value.ServiceKind,
            StringComparer.Ordinal).ToArray()));

    static bool SameTerms(IndependenceRuleSetTerms left, IndependenceRuleSetTerms right) =>
        left.LookBackMonths == right.LookBackMonths && left.Classifications.OrderBy(static value => value.ServiceKind,
            StringComparer.Ordinal).SequenceEqual(right.Classifications.OrderBy(static value => value.ServiceKind, StringComparer.Ordinal));

    static Result<IndependenceRuleSetView> Error(RequestErrorKind kind, string message) =>
        Result<IndependenceRuleSetView>.Failure(new(kind, message));
}
