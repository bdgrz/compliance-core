using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Aggregates;

/// <summary>
///     One import batch from staging to its terminal disposition. It owns the immutable staged observation, the
///     personal correlation decisions, the sealed omission proposal, the sealed complete plan, execution
///     authorization and the single publication or cancellation. Its logical revision counts the facts of this batch
///     only. A published batch never depends on the source's latest batch pointer: its own
///     <see cref="ApplicationImportCommitted" /> is the one publication gate of every target.
/// </summary>
public sealed class ApplicationImportBatch : Aggregate
{
    /// <summary>Stream area of every import batch (the realm is the tenant).</summary>
    public const string Area = "application_imports";

    readonly Uuid _tenantId;
    readonly ApplicationImportBatchNativeCut _cut = new();
    readonly Uuid _selectedRowId;
    ApplicationImportRowCorrelated? _selectedCorrelation;
    DomainEvent? _latest;
    ApplicationImportStaged? _staged;
    ApplicationImportRetirementProposalSealed? _proposal;
    ApplicationImportPlanSealed? _plan;
    ApplicationImportExecutionAuthorized? _authorized;
    ApplicationImportCancellationAccepted? _cancellationAccepted;
    ApplicationImportCommitted? _committed;
    ApplicationImportCanceled? _canceled;
    ApplicationImportFailed? _failed;
    long _revision;

    /// <summary>Opens a batch, selecting the logical operation to resolve from its accepted facts.</summary>
    public ApplicationImportBatch(Uuid tenantId, Uuid batchId, Uuid selectedLogicalId = default, Uuid selectedRowId = default)
        : base(batchId, new EventStreamAddress(tenantId.ToString(), Area, batchId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        _selectedRowId = selectedRowId;
        On<ApplicationImportStaged>(ApplyFact);
        On<ApplicationImportRowCorrelated>(ApplyFact);
        On<ApplicationImportRetirementProposalSealed>(ApplyFact);
        On<ApplicationImportPlanSealed>(ApplyFact);
        On<ApplicationImportExecutionAuthorized>(ApplyFact);
        On<ApplicationImportCancellationAccepted>(ApplyFact);
        On<ApplicationImportCommitted>(ApplyFact);
        On<ApplicationImportCanceled>(ApplyFact);
        On<ApplicationImportFailed>(ApplyFact);
    }

    /// <summary>Maximum serialized size of one staged observation, below the immutable content limit.</summary>
    public const int MaximumStagedBytes = 8 * 1024 * 1024;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>True once the observation was staged.</summary>
    public bool IsCreated => _staged is not null;

    /// <summary>The logical revision: the number of facts of this batch.</summary>
    public long Revision => _revision;

    /// <summary>The staged observation fact.</summary>
    public ApplicationImportStaged? Staged => _staged;

    /// <summary>The source key of the observation.</summary>
    public string? SourceKey => _staged?.SourceKey;

    /// <summary>The source namespace of the observation.</summary>
    public string? SourceNamespace => _staged?.SourceNamespace;

    /// <summary>The coverage declared by the observation.</summary>
    public string? Coverage => _staged?.Coverage;

    /// <summary>The content digest of the observation.</summary>
    public string? ContentDigest => _staged?.ContentSha256;

    /// <summary>The submitting member.</summary>
    public Uuid SubmitterMemberId => _staged?.ActorMemberId ?? Uuid.Empty;

    /// <summary>The display name of the submitting member.</summary>
    public string? SubmitterDisplay => _staged?.ActorDisplay;

    /// <summary>The latest sealed omission proposal.</summary>
    public ApplicationImportRetirementProposalSealed? Proposal => _proposal;

    /// <summary>The sealed plan, once accepted.</summary>
    public ApplicationImportPlanSealed? Plan => _plan;

    /// <summary>The execution authorization, once the source is claimed.</summary>
    public ApplicationImportExecutionAuthorized? Authorization => _authorized;

    /// <summary>The accepted cancellation, once requested.</summary>
    public ApplicationImportCancellationAccepted? CancellationAccepted => _cancellationAccepted;

    /// <summary>The publication fact of every target.</summary>
    public ApplicationImportCommitted? Committed => _committed;

    /// <summary>The terminal cancellation fact.</summary>
    public ApplicationImportCanceled? Canceled => _canceled;

    /// <summary>The terminal failure fact.</summary>
    public ApplicationImportFailed? Failed => _failed;

    public Uuid TenantId => _tenantId;
    public DomainEvent? Latest => _latest;
    internal Uuid SelectedRowId => _selectedRowId;
    internal ApplicationImportBatchNativeCut NativeCut => _cut;

    /// <summary>True once the batch has a terminal disposition.</summary>
    public bool IsEnded => _committed is not null || _canceled is not null || _failed is not null;

    /// <summary>True once a cancellation was accepted: the batch can no longer publish.</summary>
    public bool IsCancelRequested => _cancellationAccepted is not null || _canceled is not null;

    /// <summary>The lifecycle state readers expose.</summary>
    public string State => _failed is not null ? "failed"
        : _canceled is not null ? "canceled"
        : _committed is not null ? "committed"
        : _cancellationAccepted is not null ? "canceling"
        : _plan is not null ? "accepting"
        : "preview_ready";

    /// <summary>True when the batch is still open for personal preparation: staged, not sealed and not canceled.</summary>
    public bool IsPreparing => IsCreated && _plan is null && !IsCancelRequested && !IsEnded;

    /// <summary>The correlation decision recorded for a row, if any.</summary>
    public ApplicationImportRowCorrelated? GetCorrelation(Uuid rowId) => rowId == _selectedRowId ? _selectedCorrelation : null;

    /// <summary>True when the latest omission proposal is still current: nothing changed since it was sealed.</summary>
    public bool HasCurrentProposal => _proposal is not null && _proposal.Revision == _revision;

    /// <summary>The deterministic batch identity of a submission; unchanged from the legacy writer.</summary>
    public static Uuid BatchIdFor(StageApplicationImport request) =>
        Uuid.CreateVersion5(request.TenantId,
            $"application_import:{Part(request.SourceKey)}{Part(request.SourceNamespace)}{request.SubmissionId}");

    /// <summary>Checks that the observation can still be staged on this stream.</summary>
    public Result PrepareStage() => IsCreated
        ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "The submission ID already exists with different content."))
        : Result.Success;

    /// <summary>Stages the observation as the root native fact of its submission.</summary>
    public Result Stage(OperationProvenance operation, Uuid submissionId, string sourceKey, string sourceNamespace,
        string coverage, string contentSha256, ImmutableContentRef rowsManifest, int rowCount, int invalidCount,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset submittedAt, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareStage();
        if (!prepared.IsSuccess)
            return prepared;
        if (operation.Owner != Stream || operation.LogicalId != submissionId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Staging is the root operation of its own batch under its submission identity."));
        return Append(operation, new ApplicationImportStaged(_tenantId, Id, submissionId, sourceKey, sourceNamespace, coverage,
            contentSha256, rowsManifest, actorMemberId, actorDisplay, submittedAt, operation, firstResponse,
            operationResult, rowCount, invalidCount));
    }

    /// <summary>Checks the batch-local guards of a correlation decision.</summary>
    public Result PrepareCorrelation(long expectedRevision)
    {
        var open = RequireOpen("The import plan is already frozen.");
        return open.IsSuccess ? CheckRevision(expectedRevision) : open;
    }

    /// <summary>Records a correlation decision as the root native fact of its command.</summary>
    public Result Correlate(OperationProvenance operation, Uuid rowId, string sourceRecordId, string decision,
        Uuid applicationId, long? expectedApplicationRevision, string reason, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset recordedAt, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A correlation is a root operation of its own batch with an expected revision."));
        var prepared = PrepareCorrelation((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        return Append(operation, new ApplicationImportRowCorrelated(_tenantId, SourceKey!, SourceNamespace!, Id, _revision + 1,
            rowId, sourceRecordId, decision, applicationId, expectedApplicationRevision, reason, actorMemberId,
            actorDisplay, recordedAt, operation, firstResponse, operationResult));
    }

    /// <summary>Checks the batch-local guards of an omission proposal.</summary>
    public Result PrepareProposal(long expectedRevision)
    {
        var open = RequireOpen("The import batch is not available for a retirement proposal.");
        if (!open.IsSuccess)
            return open;
        if (HasCurrentProposal)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The retirement proposal changed or is stale."));
        return CheckRevision(expectedRevision);
    }

    /// <summary>Seals the omission proposal as the root native fact of its command.</summary>
    public Result SealProposal(OperationProvenance operation, string proposalSha256, ImmutableContentRef proposal,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A proposal is a root operation of its own batch with an expected revision."));
        var prepared = PrepareProposal((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        return Append(operation, new ApplicationImportRetirementProposalSealed(_tenantId, Id, _revision + 1, proposalSha256,
            operation, firstResponse, operationResult, proposal));
    }

    /// <summary>Checks the batch-local guards of acceptance.</summary>
    public Result PrepareAcceptance(long expectedRevision)
    {
        var open = RequireOpen("The import plan is already accepted.");
        return open.IsSuccess ? CheckRevision(expectedRevision) : open;
    }

    /// <summary>Seals the complete plan as the root native fact of its command.</summary>
    public Result SealPlan(OperationProvenance operation, string planSha256, ImmutableContentRef frozenPlan,
        long rowCount, long targetGroupCount, ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An acceptance is a root operation of its own batch with an expected revision."));
        var prepared = PrepareAcceptance((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        return Append(operation, new ApplicationImportPlanSealed(_tenantId, Id, _revision + 1, planSha256, operation, frozenPlan,
            rowCount, targetGroupCount, SourceKey!, SourceNamespace!, firstResponse, null));
    }

    /// <summary>Checks that execution can still be authorized.</summary>
    public Result PrepareExecutionAuthorization()
    {
        if (_plan is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The import has no durable personal acceptance plan."));
        return IsCancelRequested || IsEnded
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict, "The import batch is canceled or ended."))
            : Result.Success;
    }

    /// <summary>Authorizes execution after the source claim; the fact carries the sealed plan's intent unchanged.</summary>
    public Result AuthorizeExecution(OperationProvenance operation, ImmutableContentRef intent)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        var prepared = PrepareExecutionAuthorization();
        if (!prepared.IsSuccess)
            return prepared;
        if (operation.Owner != Stream || _authorized is not null || intent != _plan!.FrozenPlan)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Execution is authorized once, for the sealed plan of this batch."));
        return Append(operation, new ApplicationImportExecutionAuthorized(Stream, _revision + 1, ApplicationImportEffects.Generation,
            intent, null, operation, null, null));
    }

    /// <summary>Checks the batch-local guards of a cancellation.</summary>
    public Result PrepareCancellation(long expectedRevision)
    {
        if (!IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The import batch was not found."));
        if (_committed is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The import batch is committed and cannot be canceled."));
        if (_failed is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The import batch is failed."));
        if (IsCancelRequested)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The import batch is already canceled."));
        return CheckRevision(expectedRevision);
    }

    /// <summary>Accepts the cancellation as the root native fact of its command; the batch can no longer publish.</summary>
    public Result AcceptCancellation(OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A cancellation is a root operation of its own batch with an expected revision."));
        var prepared = PrepareCancellation((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        return Append(operation, new ApplicationImportCancellationAccepted(Stream, _revision + 1,
            ApplicationImportEffects.Generation, intent, null, operation, firstResponse, null));
    }

    /// <summary>Checks that the batch can publish: authorized, not canceled and not yet published.</summary>
    public Result PrepareCommit()
    {
        if (_plan is null || _authorized is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The import batch has no authorized execution.", true));
        if (IsCancelRequested)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The import batch is canceled."));
        return IsEnded
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict, "The import batch already ended."))
            : Result.Success;
    }

    /// <summary>Publishes every target together.</summary>
    public Result Commit(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef effectsManifest,
        ImmutableContentRef frozenRows, long sourceGeneration, DateTimeOffset committedAt, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareCommit();
        if (!prepared.IsSuccess)
            return prepared;
        return Append(operation, new ApplicationImportCommitted(_tenantId, SourceKey!, SourceNamespace!, Id, _revision + 1,
            _plan!.PlanSha256, effectsManifest, committedAt, operation, frozenRows, sourceGeneration, intent, null,
            operationResult));
    }

    /// <summary>Checks that an accepted cancellation can complete.</summary>
    public Result PrepareCancellationCompletion()
    {
        if (_cancellationAccepted is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The import batch has no accepted cancellation.", true));
        return _committed is not null || _canceled is not null || _failed is not null
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict, "The import batch already ended."))
            : Result.Success;
    }

    /// <summary>Completes the cancellation once every target is fenced.</summary>
    public Result CompleteCancellation(OperationProvenance operation, ImmutableContentRef intent, string reason,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset canceledAt, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareCancellationCompletion();
        if (!prepared.IsSuccess)
            return prepared;
        return Append(operation, new ApplicationImportCanceled(_tenantId, Id, _revision + 1, reason, actorMemberId, actorDisplay,
            canceledAt, operation, intent, null, operationResult));
    }

    Result RequireOpen(string sealedMessage)
    {
        if (!IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The import batch was not found."));
        if (IsCancelRequested || _failed is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The import batch is canceled or failed."));
        if (_committed is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The import batch is committed."));
        return _plan is not null
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict, sealedMessage))
            : Result.Success;
    }

    Result CheckRevision(long expectedRevision) => expectedRevision == _revision
        ? Result.Success
        : Result.Failure(VersionedRecordRules.StaleRevision("import", _revision).ToRequestError());

    void ApplyFact(ApplicationImportStaged ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.BatchId != Id || _staged is not null || _revision != 0 ||
            ev.Operation.Owner != Stream || ev.Operation.LogicalId != ev.SubmissionId || ev.FirstResponse is null ||
            ev.RowCount < 1 || ev.InvalidCount < 0 || ev.InvalidCount > ev.RowCount)
            throw new InvalidOperationException("An import observation is staged once on its own batch stream.");
        _staged = ev;
        _revision = 1;
        _latest = ev;
    }

    void ApplyFact(ApplicationImportRowCorrelated ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.BatchId != Id || _staged is null || ev.Revision != _revision + 1 ||
            ev.Operation.Owner != Stream || ev.FirstResponse is null || _plan is not null || IsEnded ||
            IsCancelRequested)
            throw new InvalidOperationException("A correlation follows the previous revision of an open batch.");
        if (ev.RowId == _selectedRowId)
            _selectedCorrelation = ev;
        _revision = ev.Revision;
        _latest = ev;
    }

    void ApplyFact(ApplicationImportRetirementProposalSealed ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.BatchId != Id || _staged is null || ev.Revision != _revision + 1 ||
            ev.Operation.Owner != Stream || ev.FirstResponse is null || _plan is not null || IsEnded ||
            IsCancelRequested || HasCurrentProposal)
            throw new InvalidOperationException("A proposal follows the previous revision of an open batch.");
        _proposal = ev;
        _revision = ev.Revision;
        _latest = ev;
    }

    void ApplyFact(ApplicationImportPlanSealed ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.BatchId != Id || _staged is null || ev.Revision != _revision + 1 ||
            ev.Operation.Owner != Stream || ev.FirstResponse is null || _plan is not null || IsEnded ||
            IsCancelRequested || ev.RowCount < 1 || ev.TargetGroupCount < 1 ||
            ev.SourceKey != _staged.SourceKey || ev.SourceNamespace != _staged.SourceNamespace)
            throw new InvalidOperationException("A plan is sealed once on an open staged batch.");
        _plan = ev;
        _revision = ev.Revision;
        _latest = ev;
    }

    void ApplyFact(ApplicationImportExecutionAuthorized ev)
    {
        Acceptance.Observe(ev);
        if (ev.Owner != Stream || ev.Revision != _revision + 1 || ev.Operation.Owner != Stream || _plan is null ||
            _authorized is not null || IsEnded || IsCancelRequested || ev.Intent != _plan.FrozenPlan)
            throw new InvalidOperationException("Execution is authorized once for an open sealed batch.");
        _authorized = ev;
        _revision = ev.Revision;
        _latest = ev;
    }

    void ApplyFact(ApplicationImportCancellationAccepted ev)
    {
        Acceptance.Observe(ev);
        if (ev.Owner != Stream || ev.Revision != _revision + 1 || ev.Operation.Owner != Stream ||
            ev.FirstResponse is null || _staged is null || IsEnded || IsCancelRequested)
            throw new InvalidOperationException("A cancellation is accepted once for an open batch.");
        _cancellationAccepted = ev;
        _revision = ev.Revision;
        _latest = ev;
    }

    void ApplyFact(ApplicationImportCommitted ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.BatchId != Id || ev.Revision != _revision + 1 ||
            ev.Operation.Owner != Stream || _plan is null || _authorized is null || IsEnded || IsCancelRequested ||
            ev.PlanSha256 != _plan.PlanSha256 || ev.Intent != _plan.FrozenPlan)
            throw new InvalidOperationException("A commit publishes an authorized, open sealed batch once.");
        _committed = ev;
        _revision = ev.Revision;
        _latest = ev;
    }

    void ApplyFact(ApplicationImportCanceled ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.BatchId != Id || ev.Revision != _revision + 1 ||
            ev.Operation.Owner != Stream || _cancellationAccepted is null || IsEnded)
            throw new InvalidOperationException("A cancellation completes an accepted cancellation once.");
        _canceled = ev;
        _revision = ev.Revision;
        _latest = ev;
    }

    void ApplyFact(ApplicationImportFailed ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.BatchId != Id || ev.Revision != _revision + 1 ||
            ev.Operation.Owner != Stream || _plan is null || IsEnded)
            throw new InvalidOperationException("A failure ends an open sealed batch once.");
        _failed = ev;
        _revision = ev.Revision;
        _latest = ev;
    }

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

    static string Part(string? value) =>
        $"{(value?.Length ?? -1).ToString(CultureInfo.InvariantCulture)}:{value}";

    /// <summary>The digest of the authored observation, over the trimmed source identities and rows.</summary>
    public static string ObservationSha256(StageApplicationImport request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var value = new StringBuilder();
        value.Append(Part(request.SourceKey)).Append(Part(request.SourceNamespace))
            .Append(Part(request.Coverage))
            .Append(request.Rows.Count.ToString(CultureInfo.InvariantCulture))
            .Append(':');
        foreach (var row in request.Rows)
            value.Append(Part(row.SourceRecordId)).Append(Part(row.Name?.Trim()))
                .Append(Part(row.Purpose?.Trim())).Append(Part(row.OwnerReference?.Trim()));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString())));
    }
}
