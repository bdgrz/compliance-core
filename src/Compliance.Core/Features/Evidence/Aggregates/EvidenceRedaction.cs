using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.History;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Evidence.Aggregates;

/// <summary>Owns the current exact preparation and independent approval; complete history lives outside the command owner.</summary>
public sealed class EvidenceRedaction : Aggregate
{
    readonly Uuid _tenantId;
    DomainEvent? _selectedLegacy;
    DateTimeOffset _lastChangedAt;
    bool _emitting;

    public EvidenceRedaction(Uuid tenantId, Uuid redactionId, Uuid selectedCommandId = default)
        : base(redactionId, new EventStreamAddress(tenantId.ToString(), "evidence-redactions", redactionId.ToString()))
    {
        _tenantId = tenantId;
        // A logical id cannot cross preparation/approval kinds on this owner.
        Acceptance = new NativeAcceptance(selectedCommandId);
        On<EvidenceRedactionPrepared>(ev => ApplyPreparation(ev, ev.TenantId, ev.RedactionId, ev.ExpectedRevision, ev.Preparation));
        On<EvidenceRedactionApproved>(ev => ApplyApproval(ev, ev.TenantId, ev.RedactionId, ev.ExpectedRevision, ev.Approval));
        On<EvidenceRedactionPreparedV2>(ev => ApplyPreparation(ev, ev.TenantId, ev.RedactionId, ev.ExpectedRevision, ev.Preparation));
        On<EvidenceRedactionApprovedV2>(ev => ApplyApproval(ev, ev.TenantId, ev.RedactionId, ev.ExpectedRevision, ev.Approval));
    }

    public long Revision { get; private set; }
    public EvidenceRedactionPreparationFact? CurrentPreparation { get; private set; }
    public EvidenceRedactionApprovalFact? CurrentApproval { get; private set; }
    internal DomainEvent? CurrentPreparationFact { get; private set; }
    internal NativeAcceptance Acceptance { get; }
    internal EvidenceRedactionReplayCut ReplayCut { get; } = new();
    internal bool HasSelectedLegacy => _selectedLegacy is not null;
    internal DomainEvent? SelectedLegacyFact => _selectedLegacy;
    internal DateTimeOffset LastChangedAt => _lastChangedAt;
    internal Uuid TenantId => _tenantId;

    internal Result<EvidenceRedactionPreparationFact> DecidePrepare(Uuid commandId, long expectedRevision,
        EvidenceRedactionSourceCapsule original, EvidenceRedactionSourceCapsule derived, string provenance,
        string reason, ActorReference actor, DateTimeOffset at) =>
        EvidenceRedactionRules.Prepare(_tenantId, Id, Revision, CurrentPreparation, _lastChangedAt, commandId,
            expectedRevision, original, derived, provenance, reason, actor, at);

    internal Result<EvidenceRedactionApprovalFact> DecideApprove(Uuid commandId, long expectedRevision,
        Uuid preparationId, long preparedRevision, ulong derivedPosition, ActorReference actor, DateTimeOffset at,
        DateTimeOffset availableAt, EvidenceRedactionWaiverSnapshot? waiver) =>
        EvidenceRedactionRules.Approve(_tenantId, Id, Revision, CurrentPreparation, CurrentApproval, _lastChangedAt,
            commandId, expectedRevision, preparationId, preparedRevision, derivedPosition, actor, at, availableAt, waiver);

    internal Result Append(DomainEvent fact, EvidenceRedactionPreparedCut cut)
    {
        var verified = ReplayCut.Require(this);
        if (!verified.IsSuccess)
            return verified;
        if (!EvidenceRedactionRules.Fits(fact))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The complete native redaction event exceeds its payload bound."));
        if (cut != EvidenceRedactionPreparedCut.Of(this) || HasSelectedLegacy || Acceptance.SelectedFact is not null ||
            fact is not INativeOperationFact native || native.Operation is null ||
            native.Operation.AuthoredIntentSha256 is not { Length: 64 } ||
            !NativeOwnerHistory.IsOwnRoot(this, native.Operation, fact is EvidenceRedactionPreparedV2
                ? nameof(PrepareEvidenceRedaction) : nameof(ApproveEvidenceRedaction), (ulong)Revision) ||
            native.Operation.LogicalId != Acceptance.SelectedLogicalId ||
            native.Operation.Owner != Stream || native.Operation.RootOperationId != native.Operation.LogicalId ||
            native.Operation.FirstOwner != Stream || native.Operation.AffectedOwner != Stream || native.Operation.ExpectedRevisionOwner != Stream ||
            native.Operation.Episode != RootOperation.InitialEpisode || native.FirstResponse is null || OperationResultOf(fact) is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A redaction append requires its exact selected native preparation and original response cut.", true));
        _emitting = true;
        try
        {
            RaiseEvent(fact);
        }
        finally
        {
            _emitting = false;
        }
        return Result.Success;
    }

    void ApplyPreparation(DomainEvent fact, Uuid tenantId, Uuid redactionId, long expectedRevision,
        EvidenceRedactionPreparationFact prepared)
    {
        if (tenantId != _tenantId || redactionId != Id || prepared is null)
            throw new ImmutableContentIntegrityException("The redaction preparation belongs to another owner.");
        var decided = DecidePrepare(prepared.PreparationId, expectedRevision, prepared.Original, prepared.Derived,
            prepared.Provenance, prepared.Reason, prepared.PreparedBy, prepared.PreparedAt);
        if (!decided.IsSuccess || decided.Value != prepared)
            throw new ImmutableContentIntegrityException("Redaction lineage must preserve exact immutable identities, attribution and chronology.");
        Observe(fact, prepared.PreparationId);
        (Revision, CurrentPreparation, CurrentApproval, CurrentPreparationFact, _lastChangedAt) =
            (prepared.Revision, prepared, null, fact, prepared.PreparedAt);
    }

    void ApplyApproval(DomainEvent fact, Uuid tenantId, Uuid redactionId, long expectedRevision,
        EvidenceRedactionApprovalFact approved)
    {
        if (tenantId != _tenantId || redactionId != Id || approved is null)
            throw new ImmutableContentIntegrityException("The redaction approval belongs to another owner.");
        var decided = DecideApprove(approved.ApprovalId, expectedRevision, approved.PreparationId, approved.PreparedRevision,
            approved.DerivedSourcePosition, approved.ApprovedBy, approved.ApprovedAt, approved.DerivedAvailableAt,
            approved.SeparationOfDutiesWaiver);
        if (!decided.IsSuccess || decided.Value != approved)
            throw new ImmutableContentIntegrityException("Redaction approval must preserve the exact current preparation and independent decision.");
        Observe(fact, approved.ApprovalId);
        (Revision, CurrentApproval, _lastChangedAt) = (approved.Revision, approved, approved.ApprovedAt);
    }

    void Observe(DomainEvent fact, Uuid id)
    {
        if (fact is INativeOperationFact native)
        {
            if (native.Operation is null || native.Operation.AuthoredIntentSha256 is not { Length: 64 })
                throw new ImmutableContentIntegrityException("The native redaction receipt is missing its complete operation provenance.");
            if (native.Operation.LogicalId == Acceptance.SelectedLogicalId &&
                (Acceptance.SelectedFact is not null || _selectedLegacy is not null))
                throw new ImmutableContentIntegrityException("The selected redaction receipt repeats or upgrades an occupied legacy operation.");
            Acceptance.Observe(fact);
        }
        else if (id == Acceptance.SelectedLogicalId && id != Uuid.Empty)
        {
            if (_selectedLegacy is not null)
                throw new ImmutableContentIntegrityException("The legacy redaction operation is repeated.");
            _selectedLegacy = fact;
        }
    }

    protected override void Apply(DomainEvent ev)
    {
        base.Apply(ev);
        ReplayCut.Observe(ev, _emitting);
    }

    internal static ImmutableContentRef? OperationResultOf(DomainEvent fact) => fact switch
    {
        EvidenceRedactionPreparedV2 value => value.OperationResult,
        EvidenceRedactionApprovedV2 value => value.OperationResult,
        _ => null,
    };

    internal SeparationOfDutiesWaiverScope WaiverScope(EvidenceRedactionPreparationFact prepared) =>
        EvidenceRedactionRules.WaiverScope(Id, prepared);
    internal static string IdentityDigest(EvidenceRedactionSourceCapsule source) => EvidenceRedactionRules.IdentityDigest(source);
}
