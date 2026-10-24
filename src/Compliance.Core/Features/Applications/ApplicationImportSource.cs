using System.Globalization;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     The canonical import source of one tenant, source key and namespace. It serializes admission: exactly one batch
///     holds the source at a time, and a batch is only admitted against the exact source revision its plan was frozen
///     against. A published batch adds its observations to the source's claims once its hold settles; a batch that
///     settles before it was ever admitted leaves a fence, so a delayed claim is refused. The slots are rebuilt from
///     the ordered stream alone; time never releases anything.
/// </summary>
public sealed class ApplicationImportSource : Aggregate
{
    /// <summary>Stream area of every canonical import source (the realm is the tenant).</summary>
    public const string Area = "application_import_sources";

    readonly Uuid _tenantId;
    readonly string _sourceKey;
    readonly string _sourceNamespace;
    readonly Dictionary<Uuid, ApplicationImportSourceSlot> _slots = [];
    readonly List<Uuid> _published = [];
    readonly List<DomainEvent> _facts = [];
    Uuid? _active;
    long _revision;

    /// <summary>Opens the source, selecting the logical operation to resolve from its accepted facts.</summary>
    public ApplicationImportSource(Uuid tenantId, string sourceKey, string sourceNamespace,
        Uuid selectedLogicalId = default)
        : base(IdFor(tenantId, sourceKey, sourceNamespace),
            new EventStreamAddress(tenantId.ToString(), Area, IdFor(tenantId, sourceKey, sourceNamespace).ToString()))
    {
        (_tenantId, _sourceKey, _sourceNamespace) = (tenantId, sourceKey, sourceNamespace);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ApplicationImportSourceAdmissionRecorded>(Apply);
        On<ApplicationImportSourceSettled>(Apply);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The logical revision: the number of facts of the source.</summary>
    public long Revision => _revision;

    /// <summary>The batch that currently holds the source, if any.</summary>
    public Uuid? ActiveBatchId => _active;

    /// <summary>The published batches in the order their holds settled.</summary>
    public IReadOnlyList<Uuid> PublishedBatchIds => _published;

    /// <summary>Every fact of the source in stream order.</summary>
    public IReadOnlyList<DomainEvent> Facts => _facts;

    /// <summary>The admission or fence slot of a batch, once recorded.</summary>
    public ApplicationImportSourceSlot? Find(Uuid batchId) => _slots.GetValueOrDefault(batchId);

    /// <summary>The deterministic identity of a source, unchanged from the legacy ledger.</summary>
    public static Uuid IdFor(Uuid tenantId, string sourceKey, string sourceNamespace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceNamespace);
        if (tenantId == Uuid.Empty || sourceKey.Length > 128 || sourceNamespace.Length > 128 ||
            sourceKey != sourceKey.Trim() || sourceNamespace != sourceNamespace.Trim())
            throw new ArgumentException("An application import source requires a tenant and exact bounded identities.");
        return Uuid.CreateVersion5(tenantId,
            $"application_import_source:{sourceKey.Length.ToString(CultureInfo.InvariantCulture)}:{sourceKey}{sourceNamespace.Length.ToString(CultureInfo.InvariantCulture)}:{sourceNamespace}");
    }

    /// <summary>Admits one batch against the exact source revision its plan was frozen against.</summary>
    public Result Claim(OperationProvenance operation, ImmutableContentRef intent, Uuid batchId)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        var refused = Check(batchId);
        if (refused is not null)
            return Result.Failure(refused);
        RaiseEvent(new ApplicationImportSourceAdmissionRecorded(Stream, _revision + 1,
            ApplicationImportEffects.Generation, intent, null, operation, null, null, batchId, _sourceKey,
            _sourceNamespace));
        return Result.Success;
    }

    /// <summary>
    ///     Ends the hold of one batch with its terminal disposition, or fences the batch when it was never admitted.
    /// </summary>
    public Result Settle(OperationProvenance operation, ImmutableContentRef intent, Uuid batchId,
        ApplicationImportDisposition disposition)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (_slots.GetValueOrDefault(batchId) is { Settled: not null })
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The source claim of this batch is already settled."));
        if (_slots.GetValueOrDefault(batchId) is null && disposition == ApplicationImportDisposition.Published)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A batch that never claimed the source cannot publish."));
        RaiseEvent(new ApplicationImportSourceSettled(Stream, _revision + 1, ApplicationImportEffects.Generation,
            intent, null, operation, null, null, batchId, disposition));
        return Result.Success;
    }

    /// <summary>The guard of an admission: no existing slot for the batch, no other holder, the frozen revision.</summary>
    public RequestError? Check(Uuid batchId)
    {
        if (batchId == Uuid.Empty)
            return new RequestError(RequestErrorKind.Validation, "A source claim names its batch.");
        if (_slots.ContainsKey(batchId))
            return new RequestError(RequestErrorKind.Conflict, "This batch already holds or has fenced the source.");
        return _active is not null
            ? new RequestError(RequestErrorKind.Conflict, "Another batch holds this source.", true)
            : null;
    }

    void Apply(ApplicationImportSourceAdmissionRecorded ev)
    {
        Acceptance.Observe(ev);
        if (ev.Owner != Stream || ev.Operation.Owner != Stream || ev.Revision != _revision + 1 ||
            ev.Generation != ApplicationImportEffects.Generation || ev.SourceKey != _sourceKey ||
            ev.SourceNamespace != _sourceNamespace || Check(ev.BatchId) is not null)
            throw new InvalidOperationException("A source admission follows the previous revision and an idle source.");
        _revision = ev.Revision;
        _active = ev.BatchId;
        _slots.Add(ev.BatchId, new ApplicationImportSourceSlot(ev.BatchId, ev, null));
        _facts.Add(ev);
    }

    void Apply(ApplicationImportSourceSettled ev)
    {
        Acceptance.Observe(ev);
        if (ev.Owner != Stream || ev.Operation.Owner != Stream || ev.Revision != _revision + 1 ||
            ev.Generation != ApplicationImportEffects.Generation)
            throw new InvalidOperationException("A source settlement follows the previous revision of its own source.");
        var slot = _slots.GetValueOrDefault(ev.BatchId);
        if (slot is { Settled: not null } ||
            slot is null && ev.Disposition == ApplicationImportDisposition.Published)
            throw new InvalidOperationException("A source claim is settled at most once, and only a claim publishes.");
        _revision = ev.Revision;
        _slots[ev.BatchId] = new ApplicationImportSourceSlot(ev.BatchId, slot?.Admitted, ev);
        if (_active == ev.BatchId)
            _active = null;
        if (ev.Disposition == ApplicationImportDisposition.Published)
            _published.Add(ev.BatchId);
        _facts.Add(ev);
    }
}
