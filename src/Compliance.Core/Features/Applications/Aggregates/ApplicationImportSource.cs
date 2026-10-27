using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Applications.Aggregates;

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
    readonly ApplicationImportSourceNativeCut _cut = new();
    readonly Uuid _selectedBatchId;
    ApplicationImportSourceSlot? _selectedSlot;
    ApplicationImportSourceSlot? _activeSlot;
    DomainEvent? _latest;
    long _revision;

    /// <summary>Opens the source, selecting the logical operation to resolve from its accepted facts.</summary>
    public ApplicationImportSource(Uuid tenantId, string sourceKey, string sourceNamespace,
        Uuid selectedLogicalId = default, Uuid selectedBatchId = default)
        : base(IdFor(tenantId, sourceKey, sourceNamespace),
            new EventStreamAddress(tenantId.ToString(), Area, IdFor(tenantId, sourceKey, sourceNamespace).ToString()))
    {
        (_tenantId, _sourceKey, _sourceNamespace) = (tenantId, sourceKey, sourceNamespace);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        _selectedBatchId = selectedBatchId;
        On<ApplicationImportSourceAdmissionRecorded>(ApplyFact);
        On<ApplicationImportSourceSettled>(ApplyFact);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The logical revision: the number of facts of the source.</summary>
    public long Revision => _revision;

    /// <summary>The batch that currently holds the source, if any.</summary>
    public Uuid? ActiveBatchId => _activeSlot?.BatchId;

    public Uuid TenantId => _tenantId;
    public string SourceKey => _sourceKey;
    public string SourceNamespace => _sourceNamespace;
    public DomainEvent? Latest => _latest;
    internal Uuid SelectedBatchId => _selectedBatchId;
    internal ApplicationImportSourceNativeCut NativeCut => _cut;

    /// <summary>The active slot or the one historical batch explicitly selected before hydration.</summary>
    public ApplicationImportSourceSlot? Find(Uuid batchId) => batchId == _selectedBatchId ? _selectedSlot :
        _activeSlot?.BatchId == batchId ? _activeSlot : null;

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
        if (!ValidLineage(operation, batchId, nameof(ClaimApplicationImportSource)))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The claim differs from its exact first-owner batch lineage."));
        var refused = Check(batchId);
        if (refused is not null)
            return Result.Failure(refused);
        return Append(operation, new ApplicationImportSourceAdmissionRecorded(Stream, _revision + 1,
            ApplicationImportEffects.Generation, intent, null, operation, null, null, batchId, _sourceKey,
            _sourceNamespace));
    }

    /// <summary>
    ///     Ends the hold of one batch with its terminal disposition, or fences the batch when it was never admitted.
    /// </summary>
    public Result Settle(OperationProvenance operation, ImmutableContentRef intent, Uuid batchId,
        ApplicationImportDisposition disposition)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (batchId == Uuid.Empty || !Enum.IsDefined(disposition) ||
            !ValidLineage(operation, batchId, nameof(ReleaseApplicationImportSource)))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The settlement differs from its exact first-owner batch lineage or disposition."));
        if (batchId != _selectedBatchId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A source settlement requires its batch to be selected before hydration.", true));
        if (Find(batchId) is { Settled: not null })
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The source claim of this batch is already settled."));
        if (Find(batchId) is null && disposition == ApplicationImportDisposition.Published)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A batch that never claimed the source cannot publish."));
        return Append(operation, new ApplicationImportSourceSettled(Stream, _revision + 1, ApplicationImportEffects.Generation,
            intent, null, operation, null, null, batchId, disposition));
    }

    /// <summary>The guard of an admission: no existing slot for the batch, no other holder, the frozen revision.</summary>
    public RequestError? Check(Uuid batchId)
    {
        if (batchId == Uuid.Empty)
            return new RequestError(RequestErrorKind.Validation, "A source claim names its batch.");
        if (batchId != _selectedBatchId)
            return new RequestError(RequestErrorKind.Conflict,
                "A source claim requires its batch to be selected before hydration.", true);
        if (Find(batchId) is not null)
            return new RequestError(RequestErrorKind.Conflict, "This batch already holds or has fenced the source.");
        return _activeSlot is not null
            ? new RequestError(RequestErrorKind.Conflict, "Another batch holds this source.", true)
            : null;
    }

    void ApplyFact(ApplicationImportSourceAdmissionRecorded ev)
    {
        Acceptance.Observe(ev);
        if (ev.Owner != Stream || ev.Operation.Owner != Stream || ev.Revision != _revision + 1 ||
            ev.Generation != ApplicationImportEffects.Generation || ev.SourceKey != _sourceKey ||
            ev.SourceNamespace != _sourceNamespace || ev.BatchId == Uuid.Empty || _activeSlot is not null ||
            ev.BatchId == _selectedBatchId && _selectedSlot is not null)
            throw new InvalidOperationException("A source admission follows the previous revision and an idle source.");
        _revision = ev.Revision;
        _activeSlot = new ApplicationImportSourceSlot(ev.BatchId, ev, null);
        if (ev.BatchId == _selectedBatchId)
            _selectedSlot = _activeSlot;
        _latest = ev;
    }

    void ApplyFact(ApplicationImportSourceSettled ev)
    {
        Acceptance.Observe(ev);
        if (ev.Owner != Stream || ev.Operation.Owner != Stream || ev.Revision != _revision + 1 ||
            ev.Generation != ApplicationImportEffects.Generation)
            throw new InvalidOperationException("A source settlement follows the previous revision of its own source.");
        var slot = Find(ev.BatchId);
        if (slot is { Settled: not null } ||
            slot is null && ev.Disposition == ApplicationImportDisposition.Published)
            throw new InvalidOperationException("A source claim is settled at most once, and only a claim publishes.");
        _revision = ev.Revision;
        if (ev.BatchId == _selectedBatchId)
            _selectedSlot = new ApplicationImportSourceSlot(ev.BatchId, slot?.Admitted, ev);
        if (_activeSlot?.BatchId == ev.BatchId)
            _activeSlot = null;
        _latest = ev;
    }

    bool ValidLineage(OperationProvenance operation, Uuid batchId, string kind) =>
        batchId != Uuid.Empty && operation.Owner == Stream && operation.CommandKind == kind &&
        operation.FirstOwner == new ApplicationImportBatch(_tenantId, batchId).Stream &&
        operation.AffectedOwner == Stream && operation.ExpectedRevisionOwner == Stream &&
        operation.RootOperationId != Uuid.Empty && operation.CausationEventId is not null;

    Result Append(OperationProvenance operation, DomainEvent fact)
    {
        var verified = _cut.Require(this, Acceptance, operation);
        if (!verified.IsSuccess)
            return verified;
        RaiseEvent(fact);
        return Result.Success;
    }

    protected override void Apply(DomainEvent ev)
    {
        if (!ev.Metadata.IsAudit)
            _cut.Observe(ev);
        base.Apply(ev);
    }

}
