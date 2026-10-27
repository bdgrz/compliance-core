using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.History;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.AccessReviews.Aggregates;

/// <summary>
///     One access-review campaign: its launch snapshot (as verified content), the audience and item manifest, and the
///     compact completion gate. Each item is its own <see cref="AccessReviewItem" /> stream materialized from the
///     launch manifest. The logical revision is 1 after launch and advances only on a completion admission, its
///     closure and its cancellation, so item work never changes it. A completion admitted here seals the campaign
///     against a second admission until it is closed or canceled.
/// </summary>
public sealed class AccessReviewCampaign : Aggregate
{
    /// <summary>Stream area of every campaign (the realm is the tenant).</summary>
    public const string Area = "access-review-campaigns";

    /// <summary>The status of a campaign that is not completed.</summary>
    public const string Active = "active";

    /// <summary>The status of a completed campaign.</summary>
    public const string Completed = "completed";

    readonly Uuid _tenantId;
    readonly AccessReviewReplayCut _cut = new();
    readonly Uuid _selectedRootId;
    AccessReviewCampaignCompletionAccepted? _selectedCompletion;
    AccessReviewCampaignCompletionCanceled? _selectedCanceled;
    AccessReviewDecisionBatchAdmitted? _selectedBulk;
    DomainEvent? _latest;
    AccessReviewCampaignLaunched? _launched;
    AccessReviewCampaignCompletionAccepted? _pending;
    AccessReviewCampaignCompleted? _completed;
    long _revision;

    /// <summary>Opens the campaign, selecting the logical operation to resolve.</summary>
    public AccessReviewCampaign(Uuid tenantId, Uuid campaignId, Uuid selectedLogicalId = default,
        Uuid selectedRootId = default)
        : base(campaignId, new EventStreamAddress(tenantId.ToString(), Area, campaignId.ToString()))
    {
        _tenantId = tenantId;
        _selectedRootId = selectedRootId == Uuid.Empty ? selectedLogicalId : selectedRootId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<AccessReviewCampaignLaunched>(ev =>
        {
            Acceptance.Observe(ev);
            if (_launched is not null || ev.TenantId != _tenantId || ev.CampaignId != Id ||
                ev.Operation.Owner != Stream || ev.Operation.LogicalId != Id || ev.Operation.RootOperationId != Id ||
                ev.FirstResponse is null)
                throw new InvalidOperationException("A campaign accepts exactly one launch of its own campaign.");
            _launched = ev;
            _revision = 1;
            _latest = ev;
        });
        On<AccessReviewCampaignCompletionAccepted>(ev =>
        {
            Acceptance.Observe(ev);
            if (_launched is null || _pending is not null || _completed is not null || ev.Owner != Stream ||
                ev.Operation.Owner != Stream || ev.Revision != _revision + 1 || ev.FirstResponse is null)
                throw new InvalidOperationException(
                    "A completion is admitted once at a time on a launched, uncompleted campaign.");
            if (ev.Operation.RootOperationId == _selectedRootId)
                _selectedCompletion = ev;
            _pending = ev;
            _revision = ev.Revision;
            _latest = ev;
        });
        On<AccessReviewDecisionBatchAdmitted>(ev =>
        {
            Acceptance.Observe(ev);
            if (_launched is null || _pending is not null || _completed is not null || ev.Owner != Stream ||
                ev.Operation.Owner != Stream || ev.Revision != _revision + 1 || ev.FirstResponse is null ||
                ev.Operation.RootOperationId != ev.Operation.LogicalId)
                throw new InvalidOperationException(
                    "A bulk decision is admitted once, on a launched campaign that is not sealed by a completion.");
            if (ev.Operation.RootOperationId == _selectedRootId)
                _selectedBulk = ev;
            _revision = ev.Revision;
            _latest = ev;
        });
        On<AccessReviewCampaignCompleted>(ev =>
        {
            Acceptance.Observe(ev);
            if (_pending is null || _completed is not null || ev.TenantId != _tenantId || ev.CampaignId != Id ||
                ev.Operation.Owner != Stream || ev.Revision != _revision + 1 ||
                ev.Operation.RootOperationId != _pending.Operation.RootOperationId ||
                ev.Intent != _pending.Intent)
                throw new InvalidOperationException("Only the pending completion can close the campaign.");
            _completed = ev;
            _pending = null;
            _revision = ev.Revision;
            _latest = ev;
        });
        On<AccessReviewCampaignCompletionCanceled>(ev =>
        {
            Acceptance.Observe(ev);
            if (_pending is null || _completed is not null || ev.Owner != Stream || ev.Operation.Owner != Stream ||
                ev.Revision != _revision + 1 || ev.Operation.RootOperationId != _pending.Operation.RootOperationId ||
                ev.Intent != _pending.Intent)
                throw new InvalidOperationException("Only the exact pending completion can be canceled.");
            if (ev.Operation.RootOperationId == _selectedRootId)
                _selectedCanceled = ev;
            _pending = null;
            _revision = ev.Revision;
            _latest = ev;
        });
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    internal AccessReviewReplayCut ReplayCut => _cut;

    public Uuid TenantId => _tenantId;

    public DomainEvent? Latest => _latest;

    /// <summary>The launch fact, once recorded.</summary>
    public AccessReviewCampaignLaunched? Launched => _launched;

    /// <summary>True once the campaign was launched.</summary>
    public bool IsLaunched => _launched is not null;

    /// <summary>The logical revision: 1 after launch, then one per completion or bulk admission, closure and cancellation.</summary>
    public long Revision => _revision;

    /// <summary>The admitted completion that is neither closed nor canceled, if any.</summary>
    public AccessReviewCampaignCompletionAccepted? Pending => _pending;

    /// <summary>The closing fact, once the campaign completed.</summary>
    public AccessReviewCampaignCompleted? Closed => _completed;

    /// <summary>True once the campaign completed.</summary>
    public bool IsCompleted => _completed is not null;

    /// <summary>The campaign status.</summary>
    public string Status => _completed is null ? Active : Completed;

    /// <summary>The exact bulk admission selected before hydration, if present.</summary>
    public AccessReviewDecisionBatchAdmitted? AdmittedFor(Uuid id) =>
        _selectedBulk?.Operation.RootOperationId == id ? _selectedBulk : null;

    /// <summary>The explicitly selected historical completion admission, or the current pending admission.</summary>
    public AccessReviewCampaignCompletionAccepted? AcceptedFor(Uuid id) =>
        _selectedCompletion?.Operation.RootOperationId == id ? _selectedCompletion :
        _pending?.Operation.RootOperationId == id ? _pending : null;

    /// <summary>The cancellation explicitly selected before hydration, if present.</summary>
    public AccessReviewCampaignCompletionCanceled? CanceledFor(Uuid id) =>
        _selectedCanceled?.Operation.RootOperationId == id ? _selectedCanceled : null;

    /// <summary>The closing fact of a root operation, when it is the one that closed the campaign.</summary>
    public AccessReviewCampaignCompleted? ClosedFor(Uuid rootOperationId) =>
        _completed is { } closed && closed.Operation.RootOperationId == rootOperationId ? closed : null;

    /// <summary>Checks the launch guard without raising anything.</summary>
    public Result PrepareLaunch() => _launched is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The campaign already exists with different content."));

    /// <summary>Launches the campaign as the root native fact of its operation.</summary>
    public Result Launch(OperationProvenance operation, string name, string instructions, DateTimeOffset deadline,
        Uuid snapshotId, string contentSha256, ImmutableContentRef reviewers, ImmutableContentRef items,
        ImmutableContentRef itemIntents, ActorReference launchedBy, DateTimeOffset now, Uuid? programId,
        Uuid? remediationOwnerMemberId, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.LogicalId != Id)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A launch is the logical operation of its own campaign."));
        var prepared = PrepareLaunch();
        if (!prepared.IsSuccess)
            return prepared;
        return Append(operation, new AccessReviewCampaignLaunched(_tenantId, Id, name.Trim(), instructions.Trim(), deadline,
            snapshotId, contentSha256, reviewers, items, itemIntents, launchedBy, now, programId,
            remediationOwnerMemberId, operation, firstResponse, operationResult));
    }

    /// <summary>Checks the completion admission guards without raising anything.</summary>
    public Result PrepareCompletion(long expectedRevision)
    {
        if (_launched is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The campaign was not found."));
        if (_completed is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The campaign was already completed."));
        if (_pending is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A completion of this campaign is already in progress."));
        return expectedRevision == _revision
            ? Result.Success
            : Result.Failure(VersionedRecordRules.StaleRevision("access review campaign", _revision).ToRequestError());
    }

    /// <summary>Admits the completion as the root native fact of its operation.</summary>
    public Result AcceptCompletion(OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A completion is a root operation of its own campaign with an expected revision."));
        var prepared = PrepareCompletion((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        return Append(operation, new AccessReviewCampaignCompletionAccepted(Stream, _revision + 1, AccessReviewEffects.Generation,
            intent, null, operation, firstResponse, null));
    }

    /// <summary>Checks the bulk admission guards without raising anything.</summary>
    public Result PrepareBulkAdmission(long expectedRevision)
    {
        if (_launched is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The campaign was not found."));
        if (_completed is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The campaign was already completed."));
        if (_pending is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A completion of this campaign is in progress; no bulk decision is admitted until it ends."));
        return expectedRevision == _revision
            ? Result.Success
            : Result.Failure(VersionedRecordRules.StaleRevision("access review campaign", _revision).ToRequestError());
    }

    /// <summary>
    ///     Admits the complete bulk decision as the root native fact of its operation. The campaign revision advances;
    ///     the batch it creates later starts at its own revision zero and is never compared with this one.
    /// </summary>
    public Result AdmitBulkDecision(OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A bulk decision is a root operation of its own campaign with an expected revision."));
        var prepared = PrepareBulkAdmission((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        return Append(operation, new AccessReviewDecisionBatchAdmitted(Stream, _revision + 1, AccessReviewEffects.Generation,
            intent, null, operation, firstResponse, null));
    }

    /// <summary>Closes the campaign for its pending completion; the caller proved every item was held and verified.</summary>
    public Result Close(OperationProvenance operation, ImmutableContentRef completion, ImmutableContentRef itemProofs,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_pending is null || _pending.Operation.RootOperationId != operation.RootOperationId ||
            operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending completion can close the campaign."));
        return Append(operation, new AccessReviewCampaignCompleted(_tenantId, Id, _revision + 1, completion, operation,
            AccessReviewEffects.Generation, itemProofs, _pending.Intent, null, operationResult));
    }

    /// <summary>Cancels the pending completion and fences the generation against every delayed closure.</summary>
    public Result CancelCompletion(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_pending is null || _pending.Operation.RootOperationId != operation.RootOperationId ||
            operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending completion can be canceled."));
        return Append(operation, new AccessReviewCampaignCompletionCanceled(Stream, _revision + 1, AccessReviewEffects.Generation,
            _pending.Intent, failureProof, operation, null, null));
    }
    protected override void Apply(DomainEvent ev)
    {
        if (!ev.Metadata.IsAudit)
            _cut.Observe(ev);
        base.Apply(ev);
    }

    Result Append(OperationProvenance operation, DomainEvent fact)
    {
        var finalized = _cut.Require(this, Acceptance, operation);
        if (!finalized.IsSuccess)
            return finalized;
        RaiseEvent(fact);
        return Result.Success;
    }

}
