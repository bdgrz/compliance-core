using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Current bounded authority and one selected receipt; complete reconciliation lives outside this command owner.</summary>
public sealed class AuditPopulation : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _programId;
    bool _emitting;
    public AuditPopulation(Uuid tenantId, Uuid programId, Uuid populationId, Uuid selectedCommandId = default)
        : base(populationId, new EventStreamAddress(tenantId.ToString(), "audit-populations", populationId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        Acceptance = new NativeAcceptance(selectedCommandId);
        On<AuditPopulationDefined>(fact => ObserveLegacy(fact, fact.State));
        On<AuditPopulationDefinitionRevised>(fact => ObserveLegacy(fact, fact.State));
        On<AuditPopulationGenerated>(fact => ObserveLegacy(fact, fact.State));
        On<AuditPopulationFactRecorded>(fact => ObserveLegacy(fact, fact.State));
        On<AuditPopulationRowClassified>(fact => ObserveLegacy(fact, fact.State));
        On<AuditPopulationReconciled>(fact => ObserveLegacy(fact, fact.State));
        On<AuditPopulationAmended>(fact => ObserveLegacy(fact, fact.State));
        On<AuditPopulationFrozen>(fact => ObserveLegacy(fact, fact.State));
        On<AuditPopulationExportGenerated>(fact => ObserveLegacy(fact, fact.State));
        On<AuditPopulationDefinedV3>(ObserveCurrent);
        On<AuditPopulationDefinitionRevisedV2>(ObserveCurrent);
        On<AuditPopulationGeneratedV3>(ObserveCurrent);
        On<AuditPopulationFactRecordedV3>(ObserveCurrent);
        On<AuditPopulationRowClassifiedV3>(ObserveCurrent);
        On<AuditPopulationReconciledV3>(ObserveCurrent);
        On<AuditPopulationAmendedV3>(ObserveCurrent);
        On<AuditPopulationFrozenV3>(ObserveCurrent);
        On<AuditPopulationExportGeneratedV3>(ObserveCurrent);
    }
    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _programId;
    public long Revision => Header?.Revision ?? 0;
    public AuditPopulationHeader? Header { get; private set; }
    public ImmutableContentRef? CurrentContent { get; private set; }
    internal NativeAcceptance Acceptance { get; }
    internal AuditPopulationReplayCut ReplayCut { get; } = new();

    internal Result Append(DomainEvent fact, AuditPopulationPreparedCut cut)
    {
        var verified = ReplayCut.Require(this);
        if (!verified.IsSuccess)
            return verified;
        if (cut != AuditPopulationPreparedCut.Of(this) || fact is not IAuditPopulationStateFact current ||
            current.CurrentContent.Schema != AuditPopulationContent.StateSchema ||
            current.Header.Revision != Revision + 1 || DomainEventPayload.Size(fact) > DomainEventPayload.MaximumBytes)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The population append requires the exact finalized current content and target cut.", true));
        if (fact is IAuditPopulationNativeFact native && (native.Operation is null ||
            native.Operation.AuthoredIntentSha256 is not { Length: 64 } ||
            native.Operation.LogicalId != Acceptance.SelectedLogicalId || Acceptance.SelectedFact is not null ||
            !NativeOwnerHistory.IsOwnRoot(this, native.Operation, AuditPopulationContent.CommandKind(fact),
                fact is AuditPopulationRowClassifiedV3 ? null : (ulong)Revision) ||
            native.FirstResponse is null || native.OperationResult?.Schema != "mutation-result-content.v1"))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The population decision requires its exact selected native operation and original Unit result."));
        _emitting = true;
        try { RaiseEvent(fact); }
        finally { _emitting = false; }
        return Result.Success;
    }

    void ObserveLegacy(DomainEvent fact, AuditPopulationView state)
    {
        if (state is null)
            throw new ImmutableContentIntegrityException("The occupied population fact has no complete original state.");
        ObserveHeader(AuditPopulationContent.Header(state));
        CurrentContent = null;
    }
    void ObserveCurrent<T>(T fact) where T : DomainEvent, IAuditPopulationStateFact
    {
        if (fact.TenantId != _tenantId || fact.ProgramId != _programId || fact.SubjectId != Id ||
            fact.CurrentContent is null || fact.CurrentContent.Schema != AuditPopulationContent.StateSchema)
            throw new ImmutableContentIntegrityException("The population fact differs from its exact owner or content schema.");
        ObserveHeader(fact.Header);
        CurrentContent = fact.CurrentContent;
        if (fact is IAuditPopulationNativeFact native)
        {
            if (native.Operation is null || native.Operation.AuthoredIntentSha256 is not { Length: 64 } ||
                native.Operation.LogicalId == Acceptance.SelectedLogicalId && Acceptance.SelectedFact is not null)
                throw new ImmutableContentIntegrityException("The selected population operation repeats or omits its complete provenance.");
            Acceptance.Observe(fact);
        }
    }
    void ObserveHeader(AuditPopulationHeader header)
    {
        if (header is null || header.TenantId != _tenantId || header.ProgramId != _programId || header.PopulationId != Id ||
            header.Revision != Revision + 1 || header.DefinitionId == Uuid.Empty || header.PeriodId == Uuid.Empty ||
            header.ExaminationId == Uuid.Empty || header.CloseSnapshotId == Uuid.Empty || header.CloseSnapshotSha256 is not { Length: 64 } ||
            header.ActorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(header.ActorDisplay) || header.UpdatedAt == default)
            throw new ImmutableContentIntegrityException("The bounded population authority has another identity, chronology or revision.");
        Header = header;
    }
    protected override void Apply(DomainEvent fact)
    {
        base.Apply(fact);
        ReplayCut.Observe(fact, _emitting);
    }
}
