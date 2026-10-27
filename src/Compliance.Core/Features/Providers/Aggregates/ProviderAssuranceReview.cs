using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
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
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Events;
namespace Bdgrz.Compliance.Features.Providers.Aggregates;

/// <summary>
///     One immutable personal due-diligence review: the admitted request, then either its recording or its
///     cancellation. The owner is independent of every other review; admission is the root fact and recovers forward
///     once recorded.
/// </summary>
public sealed class ProviderAssuranceReview : Aggregate
{
    /// <summary>The only generation of a review; a canceled review is never retried under its identity.</summary>
    public const long Generation = 0;

    readonly Uuid _tenantId;
    readonly Uuid _reviewId;
    ProviderReviewRequested? _requested;
    ProviderReviewRecorded? _recorded;
    ProviderReviewCanceled? _canceled;

    /// <summary>Opens the review, selecting the logical operation to resolve.</summary>
    public ProviderAssuranceReview(Uuid tenantId, Uuid reviewId, Uuid selectedLogicalId = default)
        : base(ProviderAssuranceStreams.ReviewIdentity(tenantId, reviewId),
            ProviderAssuranceStreams.Review(tenantId, reviewId))
    {
        _tenantId = tenantId;
        _reviewId = reviewId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ProviderReviewRequested>(requested =>
        {
            Acceptance.Observe(requested);
            if (_requested is not null || requested.Owner != Stream || requested.Operation.Owner != Stream ||
                requested.Operation.LogicalId != _reviewId || requested.Operation.RootOperationId != _reviewId ||
                requested.FirstResponse is null)
                throw new InvalidOperationException("A review owner admits exactly one request of its own review.");
            _requested = requested;
        });
        On<ProviderReviewRecorded>(recorded =>
        {
            Acceptance.Observe(recorded);
            if (_requested is null || _recorded is not null || _canceled is not null ||
                recorded.TenantId != _tenantId || recorded.ReviewId != _reviewId ||
                recorded.Intent != _requested.Intent || recorded.Operation.Owner != Stream ||
                recorded.Operation.RootOperationId != _reviewId || recorded.RequestId != _reviewId)
                throw new InvalidOperationException("Only the exact pending review can be recorded.");
            _recorded = recorded;
        });
        On<ProviderReviewCanceled>(canceled =>
        {
            Acceptance.Observe(canceled);
            if (_requested is null || _recorded is not null || _canceled is not null || canceled.Owner != Stream ||
                canceled.Intent != _requested.Intent || canceled.Operation.Owner != Stream ||
                canceled.Operation.RootOperationId != _reviewId)
                throw new InvalidOperationException("Only the exact pending review can be canceled.");
            _canceled = canceled;
        });
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The review this owner holds.</summary>
    public Uuid ReviewId => _reviewId;

    /// <summary>The admitted request, once accepted.</summary>
    public ProviderReviewRequested? Requested => _requested;

    /// <summary>The recording, once it happened.</summary>
    public ProviderReviewRecorded? Recorded => _recorded;

    /// <summary>The cancellation, once it happened.</summary>
    public ProviderReviewCanceled? Canceled => _canceled;

    /// <summary>True when a request was admitted and neither recorded nor canceled.</summary>
    public bool IsPending => _requested is not null && _recorded is null && _canceled is null;

    /// <summary>Checks the admission guard without raising anything.</summary>
    public Result PrepareAdmit() => _requested is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The provider review identity already exists."));

    /// <summary>Admits the review as the root native fact of its logical identity.</summary>
    public Result Admit(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId != _reviewId || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A provider review is the logical operation of its own review owner."));
        var prepared = PrepareAdmit();
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new ProviderReviewRequested(Stream, 0, Generation, intent, null, operation, firstResponse, null));
        return Result.Success;
    }

    /// <summary>Records the pending review; the report uses it requires were proven by the caller.</summary>
    public Result Complete(OperationProvenance operation, ImmutableContentRef content, long providerRevision,
        string? providerMateriality, long? assuranceReportRevision, ActorReference actor, DateTimeOffset recordedAt,
        Uuid providerId, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || !IsPending)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a pending review can be recorded, on its own stream."));
        RaiseEvent(new ProviderReviewRecorded(_tenantId, _reviewId, providerId, _reviewId, content, providerRevision,
            providerMateriality, assuranceReportRevision, actor, recordedAt, operation, _requested!.Intent, null,
            operationResult));
        return Result.Success;
    }

    /// <summary>Cancels the pending review; a recorded review is never canceled.</summary>
    public Result Cancel(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(failureProof);
        if (operation.Owner != Stream || !IsPending)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a pending review can be canceled, on its own stream."));
        RaiseEvent(new ProviderReviewCanceled(Stream, 0, Generation, _requested!.Intent, failureProof, operation, null,
            null));
        return Result.Success;
    }

    /// <summary>The provenance of an operation on this owner; the owner is its own first, affected and revision scope.</summary>
    public OperationProvenance Provenance(Uuid logicalId, Uuid rootOperationId, Uuid? causationEventId,
        string commandKind, ActorReference actor, string episode, ulong? originalExpectedRevision,
        string authoredIntentSha256, DateTimeOffset acceptedAt) =>
        new(logicalId, rootOperationId, causationEventId, commandKind, actor, Stream, episode,
            originalExpectedRevision, authoredIntentSha256, acceptedAt, Stream, Stream);
}
