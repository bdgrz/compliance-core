using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
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
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Aggregates;

/// <summary>
///     The complete nonattest service history of one client and its service epoch. Every recorded service is an
///     immutable fact in record order and advances the epoch by one; no count bounds the history. An admitted preview
///     evaluation is a root fact at the current epoch and leaves the epoch unchanged, so evaluations and later read
///     uses never contend with service records. The owner embeds <see cref="SourceUseReservations" />: a personal
///     decision that holds the exact epoch does not advance it, but a held epoch refuses further services until the
///     use is settled.
/// </summary>
public sealed class ClientServiceIndependence : Aggregate, ISourceUseOwner
{
    /// <summary>Stream area of every client service owner.</summary>
    public const string Area = "client-service-independence";

    /// <summary>The only generation of an evaluation admission.</summary>
    public const long Generation = 1;

    readonly Uuid _tenantId;
    NonattestServiceRecorded? _latest;
    ClientIndependenceEvaluationAdmitted? _selectedAdmission;
    Uuid _lastAdmissionId;
    ulong _epoch;

    /// <summary>Opens the service owner of one client tenant, selecting the logical operation to resolve.</summary>
    public ClientServiceIndependence(Uuid tenantId, Uuid selectedLogicalId = default, Uuid selectedEvaluationId = default)
        : base(AggregateIdentity.Create(nameof(ClientServiceIndependence), tenantId),
            new EventStreamAddress(tenantId.ToString(), Area, tenantId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        SelectedEvaluationId = selectedEvaluationId == Uuid.Empty ? selectedLogicalId : selectedEvaluationId;
        Uses = new SourceUseReservations(Stream);
        On<NonattestServiceRecorded>(recorded =>
        {
            Acceptance.Observe(recorded);
            if (recorded.TenantId != _tenantId || recorded.Operation.Owner != Stream ||
                recorded.OperationId != recorded.Operation.LogicalId || recorded.OperationId == Uuid.Empty ||
                recorded.Operation.LogicalId != recorded.Operation.RootOperationId ||
                recorded.Revision != _epoch + 1 || recorded.FirstResponse is null ||
                recorded.OperationResult is null || recorded.OperationId == _latest?.OperationId || recorded.OperationId == _lastAdmissionId)
                throw new InvalidOperationException(
                    "A service is appended once, to its own client, at the next service epoch.");
            _latest = recorded;
            _epoch++;
        });
        On<ClientIndependenceEvaluationAdmitted>(admitted =>
        {
            Acceptance.Observe(admitted);
            if (admitted.Owner != Stream || admitted.Operation.Owner != Stream ||
                admitted.Operation.LogicalId != admitted.Operation.RootOperationId ||
                admitted.Revision != (long)_epoch || admitted.Generation != Generation ||
                admitted.FirstResponse is null || admitted.Operation.LogicalId == _lastAdmissionId ||
                admitted.Operation.LogicalId == _latest?.OperationId)
                throw new InvalidOperationException(
                    "An evaluation is admitted once, at the current service epoch of its own client.");
            _lastAdmissionId = admitted.Operation.LogicalId;
            if (admitted.Operation.LogicalId == SelectedEvaluationId)
                _selectedAdmission = admitted;
        });
        On<ClientServicePersonalDecisionUseAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (granted.Revision != (long)_epoch)
                throw new InvalidOperationException("A service epoch is held exactly at its current epoch.");
            Uses.ApplyGranted(SourceUsePurpose.EngagementPersonalDecision, granted);
        });
        On<ClientServicePersonalDecisionUseSettled>(settled =>
        {
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.EngagementPersonalDecision, settled);
        });
        On<EngagementServiceEpochAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (granted.Revision != (long)_epoch)
                throw new InvalidOperationException("A service epoch is held exactly at its current epoch.");
            Uses.ApplyGranted(SourceUsePurpose.EngagementAcceptance, granted);
        });
        On<EngagementServiceEpochSettled>(settled =>
        {
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.EngagementAcceptance, settled);
        });
    }

    /// <summary>The active grants, receiver generation and fences of this owner's source uses.</summary>
    public SourceUseReservations Uses { get; }

    /// <summary>The client tenant this owner serves.</summary>
    public Uuid TenantId => _tenantId;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The latest recorded service, whose event pins the complete history; null at epoch zero.</summary>
    public NonattestServiceRecorded? LatestService => _latest;

    /// <summary>The service epoch: the number of services ever recorded for this client.</summary>
    public ulong Epoch => _epoch;

    /// <summary>The one evaluation deliberately selected for this hydration.</summary>
    public Uuid SelectedEvaluationId { get; }

    /// <summary>The explicitly selected original evaluation admission.</summary>
    public ClientIndependenceEvaluationAdmitted? AdmissionOf(Uuid evaluationId) =>
        evaluationId == SelectedEvaluationId ? _selectedAdmission : null;

    internal FirmIndependenceReplayCut ReplayCut { get; } = new();
    internal Uuid LastAdmissionId => _lastAdmissionId;

    /// <summary>Checks the service guards without raising anything.</summary>
    public Result PrepareService(Uuid serviceRecordId, long expectedEpoch)
    {
        if (Acceptance.SelectedLogicalId == serviceRecordId && Acceptance.SelectedFact is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Service identities are immutable and cannot be reused."));
        if (Uses.HasActiveUse)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A personal decision holds the service epoch of this client; retry after it settles.", true));
        return expectedEpoch == (long)Epoch
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Reload the service epoch of this client before recording another service."));
    }

    /// <summary>Appends one service as the root native fact of its operation and advances the epoch.</summary>
    public Result RecordService(OperationProvenance operation, ImmutableContentRef serviceContent,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(serviceContent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        ArgumentNullException.ThrowIfNull(operationResult);
        if (_tenantId == Uuid.Empty || operation.OriginalExpectedRevision is not { } expected ||
            string.IsNullOrEmpty(operation.AuthoredIntentSha256) ||
            !NativeOwnerHistory.IsOwnRoot(this, operation, nameof(RecordNonattestService), expected))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A service is the logical operation of its own record on its client's owner."));
        var prepared = PrepareService(operation.LogicalId, (long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new NonattestServiceRecorded(_tenantId, operation.LogicalId, serviceContent, operation, Epoch + 1,
            firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Checks the evaluation admission guards without raising anything.</summary>
    public Result PrepareEvaluation(Uuid evaluationId, long expectedEpoch)
    {
        if (Acceptance.SelectedLogicalId == evaluationId && Acceptance.SelectedFact is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Evaluation identities are immutable and cannot be reused."));
        return expectedEpoch == (long)Epoch
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Reload the service epoch of this client before evaluating it."));
    }

    /// <summary>
    ///     Admits the evaluation as the root native fact of its operation. The frozen intent must have captured exactly
    ///     the epoch this owner holds, otherwise a service was recorded while the intent was prepared.
    /// </summary>
    public Result AdmitEvaluation(OperationProvenance operation, ImmutableContentRef intent, ulong intentEpoch,
        ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (_tenantId == Uuid.Empty || operation.OriginalExpectedRevision is not { } expected ||
            string.IsNullOrEmpty(operation.AuthoredIntentSha256) ||
            !NativeOwnerHistory.IsOwnRoot(this, operation, nameof(EvaluateClientIndependence), expected))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An evaluation is the logical operation of its own preview on its client's owner."));
        var prepared = PrepareEvaluation(operation.LogicalId, (long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        if (intentEpoch != Epoch)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A service was recorded while the evaluation was prepared; reload and retry.", true));
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new ClientIndependenceEvaluationAdmitted(Stream, (long)Epoch, Generation, intent, null, operation,
            firstResponse, null));
        return Result.Success;
    }

    /// <summary>Holds the exact complete service history for a personal decision; the epoch does not advance.</summary>
    public Result AuthorizePersonalDecisionUse(OperationProvenance operation, ImmutableContentRef intent,
        long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A service history is held on its own client stream."));
        Uses.RequireForAppend(SourceUsePurpose.EngagementPersonalDecision, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new ClientServicePersonalDecisionUseAuthorized(Stream, (long)Epoch, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>Settles a personal decision use, or fences its key when no grant exists yet.</summary>
    public Result SettlePersonalDecisionUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement is recorded on the held client stream."));
        Uses.RequireForAppend(SourceUsePurpose.EngagementPersonalDecision, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new ClientServicePersonalDecisionUseSettled(Stream, (long)Epoch, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>Holds the exact complete service history for an acceptance or staff assignment; the epoch does not advance.</summary>
    public Result AuthorizeEngagementEpoch(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A service history is held on its own client stream."));
        Uses.RequireForAppend(SourceUsePurpose.EngagementAcceptance, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new EngagementServiceEpochAuthorized(Stream, (long)Epoch, generation, intent, null, operation,
            null, null));
        return Result.Success;
    }

    /// <summary>Settles an engagement epoch hold, or fences its key when no grant exists yet.</summary>
    public Result SettleEngagementEpoch(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement is recorded on the held client stream."));
        Uses.RequireForAppend(SourceUsePurpose.EngagementAcceptance, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new EngagementServiceEpochSettled(Stream, (long)Epoch, generation, intent, null, operation, null,
            null));
        return Result.Success;
    }
    protected override void Apply(DomainEvent ev)
    {
        if (!ev.Metadata.IsAudit)
            ReplayCut.Observe(ev);
        base.Apply(ev);
        Uses.ObserveOwnerFact(ev);
    }
}
