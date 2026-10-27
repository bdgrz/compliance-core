using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Aggregates;

public sealed partial class DeclaredApplication
{
    /// <summary>Stream area of every declared application (the realm is the tenant).</summary>
    public const string Area = "applications";

    readonly Dictionary<Uuid, ApplicationImportHold> _importHolds = [];

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>True while a held import group reserves the identity of an application it will create.</summary>
    public bool ReservesIdentity => !_created && _importHolds.Values.Any(hold => hold.IsActive &&
        hold.Reserved!.InitialApplicationRevision == 0);

    /// <summary>The hold or fence this application recorded for one import batch, if any.</summary>
    public ApplicationImportHold? FindImportHold(Uuid batchId) => _importHolds.GetValueOrDefault(batchId);

    /// <summary>Every import hold and fence of this application in the order its batch first touched it.</summary>
    public IReadOnlyCollection<ApplicationImportHold> ImportHolds => _importHolds.Values;

    /// <summary>The provisional effects of every batch, in stream order.</summary>
    public IReadOnlyList<ApplicationImportEffectPending> GetPendingImportEffects() =>
        Array.AsReadOnly(_importHolds.Values.SelectMany(hold => hold.Effects).ToArray());

    /// <summary>The provisional effect of one row of one batch, if recorded.</summary>
    public ApplicationImportEffectPending? GetPendingImportEffect(Uuid batchId, Uuid rowId) =>
        _importHolds.GetValueOrDefault(batchId)?.Effects.FirstOrDefault(effect => effect.Row.RowId == rowId);

    /// <summary>
    ///     A local write conflicts with any import hold that is not settled: no local create, revise, retire or
    ///     relationship change bypasses a provisional hold.
    /// </summary>
    public RequestError? CheckPendingImportChanges() => _importHolds.Values.Any(hold => hold.IsActive)
        ? new RequestError(RequestErrorKind.Conflict,
            "The application has an unsettled import effect. Retry after the batch settles.", isTransient: true)
        : null;

    /// <summary>The guard of a hold: no fence, no other hold, and the frozen baseline revision.</summary>
    public RequestError? CheckReserve(Uuid batchId, ulong initialRevision)
    {
        if (_importHolds.ContainsKey(batchId))
            return new RequestError(RequestErrorKind.Conflict, "This batch already holds or has fenced the application.");
        if (_importHolds.Values.Any(hold => hold.IsActive))
            return new RequestError(RequestErrorKind.Conflict,
                "The application is held by another import batch. Retry after the batch settles.", isTransient: true);
        if (_retired || (initialRevision == 0) == _created || (long)initialRevision != _revision)
            return new RequestError(RequestErrorKind.Conflict,
                "The governed import target changed after its frozen plan.");
        return null;
    }

    /// <summary>Grants the exclusive hold of one batch group at its frozen baseline.</summary>
    public Result Reserve(OperationProvenance operation, ImmutableContentRef intent, Uuid batchId, Uuid applicationId,
        ulong initialRevision, ulong candidateRevision)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        var refused = CheckReserve(batchId, initialRevision);
        if (refused is not null)
            return Result.Failure(refused);
        RaiseEvent(new ApplicationImportTargetAuthorized(Stream, _revision, ApplicationImportEffects.Generation,
            intent, null, operation, null, null, batchId, applicationId, initialRevision, candidateRevision));
        return Result.Success;
    }

    /// <summary>
    ///     Records the whole frozen group as one preflighted native append batch: a provisional effect for every row
    ///     in original row order, compared once against the held baseline. Nothing is published.
    /// </summary>
    public Result ApplyImportGroup(Uuid batchId, long planRevision, string planSha256, ImmutableContentRef frozenPlan,
        ImmutableContentRef intent, IReadOnlyList<ApplicationImportEffectInput> rows, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(frozenPlan);
        ArgumentNullException.ThrowIfNull(intent);
        if (_importHolds.GetValueOrDefault(batchId) is not { IsActive: true } hold || hold.Fence is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The batch holds no active, unfenced reservation on this application.", true));
        if (hold.IsApplied || rows.Count == 0)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The import group is already applied or empty."));
        var fold = ApplicationImportGroupFold.Fold(_created, _retired, _revision, rows.Select(row => row.Row).ToArray());
        if (!fold.IsSuccess)
            return Result.Failure(fold.Error!);
        for (var index = 0; index < rows.Count; index++)
            if (fold.Value![index] != rows[index].ResultingRevision)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The frozen candidate revision differs from the group's native outcome."));
        foreach (var input in rows)
            RaiseEvent(new ApplicationImportEffectPending(_tenantId, Id, planRevision, planSha256, frozenPlan,
                input.Row, recordedAt, input.Operation, intent, null, null, batchId, input.ResultingRevision));
        return Result.Success;
    }

    /// <summary>Records the batch's fence, even when its hold was never acquired.</summary>
    public Result Fence(OperationProvenance operation, ImmutableContentRef intent, Uuid batchId, Uuid applicationId)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (_importHolds.GetValueOrDefault(batchId) is { Fence: not null } or { Settled: not null })
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The batch is already fenced or settled on this application."));
        RaiseEvent(new ApplicationImportEffectCanceled(Stream, _revision, ApplicationImportEffects.Generation,
            intent, null, operation, null, null, batchId, applicationId));
        return Result.Success;
    }

    /// <summary>
    ///     Ends the hold with the batch's exact terminal disposition. A published disposition promotes the frozen
    ///     candidate without a second content revision increment.
    /// </summary>
    public Result Settle(OperationProvenance operation, ImmutableContentRef intent, Uuid batchId, Uuid applicationId,
        ApplicationImportDisposition disposition)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        var hold = _importHolds.GetValueOrDefault(batchId);
        if (hold is null || hold.Settled is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The batch holds no unsettled reservation or fence on this application."));
        if (disposition == ApplicationImportDisposition.Published && (hold.Reserved is null || !hold.IsApplied))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an applied hold can publish.", true));
        var published = disposition == ApplicationImportDisposition.Published
            ? (long)hold.Reserved!.CandidateContentRevision
            : _revision;
        RaiseEvent(new ApplicationImportTargetSettled(Stream, published, ApplicationImportEffects.Generation, intent,
            null, operation, null, null, batchId, applicationId, disposition));
        return Result.Success;
    }

    /// <summary>
    ///     Replays one recorded stream fact into this instance, in stream order. Read composition only: the fixed-cut
    ///     reader rebuilds the application from the facts below its cut; nothing is raised or appended.
    /// </summary>
    public void Replay(DomainEvent recorded) => Apply(recorded);

    /// <summary>
    ///     Read composition only: the in-memory application as a published batch shows it, before the target hold
    ///     settles. It never raises a fact and never touches a stream.
    /// </summary>
    public void ShowPublished(Uuid batchId)
    {
        if (_importHolds.GetValueOrDefault(batchId) is { IsActive: true, IsApplied: true } hold)
            Promote(hold);
    }

    void RegisterImportEffectEvents()
    {
        On<ApplicationImportTargetAuthorized>(ev =>
        {
            Acceptance.Observe(ev);
            if (ev.Owner != Stream || ev.Operation.Owner != Stream || ev.Generation != ApplicationImportEffects.Generation ||
                ev.ApplicationId != Id || ev.BatchId == Uuid.Empty || CheckReserve(ev.BatchId, ev.InitialApplicationRevision) is not null)
                throw new InvalidOperationException("The import target hold does not belong to an available target.");
            _importHolds.Add(ev.BatchId, new ApplicationImportHold(ev.BatchId, ev, null, [], null));
        });
        On<ApplicationImportEffectPending>(ev =>
        {
            Acceptance.Observe(ev);
            if (ev.TenantId != _tenantId || ev.ApplicationId != Id || ev.Row.ApplicationId != Id ||
                ev.Operation.Owner != Stream || ev.BatchId == Uuid.Empty || ev.Row.RowId == Uuid.Empty ||
                ev.Row.Decision is not ("create_new" or "link_existing" or "retire") ||
                _importHolds.GetValueOrDefault(ev.BatchId) is not { IsActive: true, Fence: null } hold ||
                hold.Effects.Any(effect => effect.Row.RowId == ev.Row.RowId))
                throw new InvalidOperationException("The pending import effect does not belong to its held target.");
            _importHolds[ev.BatchId] = hold with { Effects = [.. hold.Effects, ev] };
        });
        On<ApplicationImportEffectCanceled>(ev =>
        {
            Acceptance.Observe(ev);
            if (ev.Owner != Stream || ev.Operation.Owner != Stream || ev.ApplicationId != Id ||
                _importHolds.GetValueOrDefault(ev.BatchId) is { Fence: not null } or { Settled: not null })
                throw new InvalidOperationException("The import fence follows an unfenced, unsettled batch.");
            _importHolds[ev.BatchId] = (_importHolds.GetValueOrDefault(ev.BatchId) ??
                new ApplicationImportHold(ev.BatchId, null, null, [], null)) with
            { Fence = ev };
        });
        On<ApplicationImportTargetSettled>(ev =>
        {
            Acceptance.Observe(ev);
            if (ev.Owner != Stream || ev.Operation.Owner != Stream || ev.ApplicationId != Id ||
                _importHolds.GetValueOrDefault(ev.BatchId) is not { Settled: null } hold)
                throw new InvalidOperationException("The import settlement follows an unsettled hold or fence.");
            var settled = hold with { Settled = ev };
            _importHolds[ev.BatchId] = settled;
            if (ev.Disposition == ApplicationImportDisposition.Published)
            {
                if (hold.Reserved is null || !hold.IsApplied)
                    throw new InvalidOperationException("Only an applied hold publishes.");
                Promote(hold);
            }
        });
    }

    void Promote(ApplicationImportHold hold)
    {
        foreach (var effect in hold.Effects)
        {
            switch (effect.Row.Decision)
            {
                case "create_new" when !_created:
                    _created = true;
                    _initialName = effect.Row.Name.Trim();
                    _initialPurpose = effect.Row.Purpose.Trim();
                    _initialOwnerReference = NormalizeOptional(effect.Row.OwnerReference);
                    _revision = effect.ResultingRevision;
                    break;
                case "retire" when !_retired:
                    _retired = true;
                    _revision = effect.ResultingRevision;
                    break;
            }
        }
    }
}
