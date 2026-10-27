using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Bounded current and explicit selected attempt authority; full attempts remain external immutable history.</summary>
public sealed class WeeklyWorkDigest : Aggregate
{
    public const string SchedulerProcessId = WeeklyWorkDigestRules.SchedulerProcessId;
    public const string DeliveryProcessId = WeeklyWorkDigestRules.DeliveryProcessId;
    public const string Area = "weekly-work-digests";
    readonly Uuid _tenant;
    readonly int _selectedAttempt;
    WorkDigestDeliveryAttemptView? _latest;
    WorkDigestDeliveryAttemptView? _selected;
    public WeeklyWorkDigest(Uuid tenantId, Uuid digestId, int selectedAttempt = 0,
        Uuid selectedCommandId = default, string? selectedCommandKind = null)
        : base(digestId, new EventStreamAddress(tenantId.ToString(), Area, digestId.ToString()))
    {
        (_tenant, _selectedAttempt) = (tenantId, selectedAttempt);
        Acceptance = new NativeAcceptance(selectedCommandId, selectedCommandKind);
        On<WeeklyWorkDigestScheduled>(ApplyFact);
        On<WeeklyWorkDigestDeliveryRequested>(ApplyFact);
        On<WeeklyWorkDigestDeliveryRecorded>(ApplyFact);
        On<WeeklyWorkDigestDeliverySuppressed>(ApplyFact);
    }
    public Uuid TenantId => _tenant;
    public NativeAcceptance Acceptance { get; }
    public WeeklyWorkDigestHeader? Header { get; private set; }
    public long Revision => Header?.Revision ?? 0;
    public string Status => Header?.Status ?? "missing";
    public Uuid MemberId => Header?.MemberId ?? Uuid.Empty;
    public DateOnly WeekStarting => Header?.WeekStarting ?? default;
    public WorkDigestDeliveryAttemptView? LatestAttempt => _latest;
    public WorkDigestDeliveryAttemptView? SelectedAttempt => _selectedAttempt == _latest?.Number ? _latest : _selected;
    public int SelectedAttemptNumber => _selectedAttempt;
    public DomainEvent? Latest { get; private set; }
    internal WorkDigestReplayCut ReplayCut { get; } = new();
    public static Uuid IdFor(Uuid tenantId, Uuid memberId, DateOnly weekStarting) =>
        WeeklyWorkDigestRules.IdFor(tenantId, memberId, weekStarting);
    public static string DeliveryKeyFor(Uuid digestId, int attemptNumber) =>
        WeeklyWorkDigestRules.DeliveryKeyFor(digestId, attemptNumber);

    internal WeeklyWorkDigestRules Rules()
    {
        var rules = new WeeklyWorkDigestRules(_tenant, Id);
        if (Header is { } header)
            rules.Restore(header, _selected, _latest);
        return rules;
    }
    Result Change(Func<WeeklyWorkDigestRules, Result> operation)
    {
        if (!ReplayCut.Allows(this))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact weekly digest source cut is not finalized; retry.", true));
        var rules = Rules();
        var result = operation(rules);
        if (result.IsSuccess && rules.PendingEvents.Count > 0)
            RaiseEvent(rules.PendingEvents.Single());
        return result;
    }
    public Result Schedule(Uuid memberId, DateOnly weekStarting, string key, ActorReference by, DateTimeOffset at) =>
        Change(rules => rules.Schedule(memberId, weekStarting, key, by, at));
    internal Result<WeeklyDigestPreparation> Prepare(WeeklyDigestMutationContent change)
    {
        if (!ReplayCut.Allows(this))
            return Result<WeeklyDigestPreparation>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact weekly source cut is not finalized; retry.", true));
        if (Header is null || change.Before != Header || change.SelectedBefore != SelectedAttempt ||
            change.LatestBefore != LatestAttempt || WeeklyDigestNative.Attempt(change) != SelectedAttemptNumber)
            return Result<WeeklyDigestPreparation>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The weekly mutation differs from its exact current/selected authority."));
        var rules = Rules();
        var evaluated = WeeklyDigestNative.Evaluate(rules, change);
        if (!evaluated.IsSuccess)
            return Result<WeeklyDigestPreparation>.Failure(evaluated.Error!);
        var prepared = change with { NoChange = rules.PendingEvents.Count == 0 };
        return Result<WeeklyDigestPreparation>.Success(new WeeklyDigestPreparation(Version, CommittedStreamPosition, prepared));
    }
    internal Result Append(WeeklyDigestPreparation prepared, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef body, ImmutableContentRef first, ImmutableContentRef result)
    {
        if (!ReplayCut.Allows(this) || Version != prepared.NativeRevision || CommittedStreamPosition != prepared.PhysicalPosition)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The weekly source changed while its receiver was prepared.", true));
        var checkedPreparation = Prepare(prepared.Change);
        if (!checkedPreparation.IsSuccess)
            return Result.Failure(checkedPreparation.Error!);
        if (checkedPreparation.Value != prepared || Acceptance.SelectedLogicalId != operation.LogicalId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The weekly receipt differs from its selected command and exact preparation."));
        var fact = WeeklyDigestNative.Fact(prepared.Change, operation, change, body, first, result);
        WeeklyDigestNative.Structural(this, fact, Revision);
        WeeklyDigestNative.VerifyPrepared(fact, prepared.Change);
        if (DomainEventPayload.Size(fact) > DomainEventPayload.MaximumBytes)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The weekly native receipt exceeds the business frame limit."));
        RaiseEvent(fact);
        return Result.Success;
    }
    void ApplyFact(DomainEvent fact)
    {
        var rules = Rules();
        try
        {
            if (fact is IWeeklyWorkDigestOperationFact native)
            {
                WeeklyDigestNative.Structural(this, fact, Revision);
                Acceptance.Observe(fact);
                if (native.NoChange)
                    rules.AdvanceReceiptRevision(native.Revision);
                else
                    rules.Replay(fact);
            }
            else
                rules.Replay(fact);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            throw new ImmutableContentIntegrityException("The weekly digest native transition is invalid: " + exception.Message);
        }
        var view = rules.Read();
        Header = new WeeklyWorkDigestHeader(view.MemberId, view.WeekStarting, view.ScheduleKey, view.ScheduledAt,
            view.ScheduledBy, view.Status, view.Revision, view.SuppressionReason);
        _latest = view.DeliveryAttempts.Count == 0 ? null : view.DeliveryAttempts[^1];
        if (_selectedAttempt > 0 && view.DeliveryAttempts.FirstOrDefault(item => item.Number == _selectedAttempt) is { } selected)
            _selected = selected;
        Latest = fact;
        ReplayCut.Observe(fact);
    }
}
