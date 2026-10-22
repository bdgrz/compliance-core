using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Fences the legacy remediation stream without rebuilding its program-wide finding state.
///     It retains only the stream identity and migration ID; the finding facts remain in the
///     append-only source history for the bounded migration reader.
/// </summary>
sealed class RemediationMigrationFenceAggregate : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _programId;
    Uuid? _migrationId;

    public RemediationMigrationFenceAggregate(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(),
            FindingAggregate.LegacySourceArea, programId.ToString()))
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty)
            throw new ArgumentException("A remediation fence needs tenant and program IDs.");

        _tenantId = tenantId;
        _programId = programId;
        On<RemediationPartitioningStarted>(ApplyPartitioningStarted);
    }

    protected override void Apply(DomainEvent ev)
    {
        switch (ev)
        {
            case FindingRaised fact:
                ObserveLegacyFact(fact.TenantId, fact.ProgramId, fact.FindingId);
                break;
            case FindingRevised fact:
                ObserveLegacyFact(fact.TenantId, fact.ProgramId, fact.FindingId);
                break;
            case CorrectiveActionAdded fact:
                ObserveLegacyFact(fact.TenantId, fact.ProgramId, fact.FindingId);
                break;
            case CorrectiveActionCompleted fact:
                ObserveLegacyFact(fact.TenantId, fact.ProgramId, fact.FindingId);
                break;
            case FindingAcceptanceLinked fact:
                ObserveLegacyFact(fact.TenantId, fact.ProgramId, fact.FindingId);
                break;
            case FindingClosed fact:
                ObserveLegacyFact(fact.TenantId, fact.ProgramId, fact.FindingId);
                break;
            case FindingReopened fact:
                ObserveLegacyFact(fact.TenantId, fact.ProgramId, fact.FindingId);
                break;
            default:
                base.Apply(ev);
                break;
        }
    }

    public CommandFailure? BeginPartitioning(Uuid migrationId, ActorReference actor,
        DateTimeOffset at, ulong? expectedPreFenceProgramVersion = null)
    {
        if (migrationId == Uuid.Empty || !ValidActor(actor) || at == default ||
            at.Offset != TimeSpan.Zero)
            return CommandFailure.InvalidContent("The remediation partitioning fence is invalid.");
        if (_migrationId is { } existing)
            return existing == migrationId
                ? null
                : CommandFailure.StateConflict(
                    "A different finding migration has already fenced this program.");
        if (expectedPreFenceProgramVersion is { } expected && expected != Version)
            return CommandFailure.StateConflict(
                "The legacy remediation stream changed after its migration source was observed.");

        RaiseEvent(new RemediationPartitioningStarted(_tenantId, _programId, migrationId,
            Version, actor, at));
        return null;
    }

    void ApplyPartitioningStarted(RemediationPartitioningStarted ev)
    {
        if (ev.TenantId != _tenantId || ev.ProgramId != _programId ||
            ev.MigrationId == Uuid.Empty || ev.PreFenceProgramVersion != Version ||
            _migrationId is not null || !ValidActor(ev.StartedBy) || ev.StartedAt == default ||
            ev.StartedAt.Offset != TimeSpan.Zero)
            throw new InvalidOperationException(
                "The remediation fence is duplicated, out of scope, or does not identify the exact source stream head.");

        _migrationId = ev.MigrationId;
    }

    void ObserveLegacyFact(Uuid tenantId, Uuid programId, Uuid findingId)
    {
        if (tenantId != _tenantId || programId != _programId || findingId == Uuid.Empty)
            throw new InvalidOperationException(
                "A legacy finding fact does not belong to the remediation stream being fenced.");
    }

    static bool ValidActor(ActorReference? actor) => actor is
    { Kind: "system_process", Id.Length: > 0, Display.Length: > 0 };
}
