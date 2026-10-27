using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Projections;
using Bdgrz.Compliance.Features.Operations.Readers;
using Cntryl.Fitz;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

sealed class FitzObservationPeriodDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/observation-period-identity-directory/projection-v1", ProjectorName),
        IObservationPeriodDirectoryReader, IObservationPeriodCloseDirectoryReader, IObservationPeriodDirectoryProjection
{
    public const string ProjectorName = "ObservationPeriodIdentityDirectoryV1";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), TypeIIPeriod.Area)), ct);

    public async ValueTask ApplyAsync(ObservationPeriodStarted fact, CancellationToken ct = default)
    {
        if (fact.TenantId == Uuid.Empty || fact.Header.ProgramId == Uuid.Empty || fact.SubjectId == Uuid.Empty ||
            fact.Revision != 1 || fact.OccurredAt == default ||
            fact.Operation.Owner != new TypeIIPeriod(fact.TenantId, fact.Header.ProgramId, fact.SubjectId).Stream ||
            fact.Operation.RootOperationId != fact.SubjectId || fact.CalendarAuthorizationId != fact.SubjectId ||
            fact.Header.TenantId != fact.TenantId || fact.Header.PeriodId != fact.SubjectId || fact.Header.Revision != 1)
            throw new InvalidOperationException("A period identity must come from its exact native creation.");
        var identity = new ObservationPeriodIdentity(fact.TenantId, fact.Header.ProgramId, fact.SubjectId,
            fact.OccurredAt);
        var current = await ObservationPeriodDirectorySchema.Periods.GetAsync(Transaction, fact.SubjectId, ct)
            .ConfigureAwait(false);
        if (current is not null)
        {
            if (current != identity)
                throw new InvalidOperationException("A period identity cannot change its original program or creation ordering.");
            return;
        }
        await ObservationPeriodDirectorySchema.Periods.InsertAsync(Transaction, identity, ct).ConfigureAwait(false);
    }

    public ValueTask ApplyAsync(ObservationPeriodCloseFrozen fact, CancellationToken ct = default) => CloseAsync(fact, fact, ct);
    public ValueTask ApplyAsync(TypeIIPeriodCloseAmended fact, CancellationToken ct = default) => CloseAsync(fact, fact, ct);

    async ValueTask CloseAsync(DomainEvent value, ITypeIIPeriodStateFact fact, CancellationToken ct)
    {
        var header = fact.Header;
        if (header.TenantId != fact.TenantId || header.PeriodId != fact.SubjectId || header.ProgramId == Uuid.Empty ||
            header.Status != "closed" || header.LatestCloseSnapshotId is not { } close || close == Uuid.Empty ||
            header.LatestCloseVersion is not > 0 || header.LatestCloseSha256 is not { Length: 64 } digest ||
            fact.Operation.Owner != new TypeIIPeriod(fact.TenantId, header.ProgramId, fact.SubjectId).Stream)
            throw new InvalidOperationException("A close locator requires its exact native period authority.");
        var identity = new ObservationPeriodCloseIdentity(fact.TenantId, header.ProgramId, close, fact.SubjectId,
            header.LatestCloseVersion.Value, fact.Revision, digest, NativeFactReference.Of(fact.Operation.Owner, value));
        var current = await ObservationPeriodDirectorySchema.Closes.GetAsync(Transaction,
            new ObservationPeriodDirectorySchema.CloseKey(close, fact.SubjectId), ct).ConfigureAwait(false);
        if (current is not null)
        {
            if (current != identity)
                throw new InvalidOperationException("A close locator cannot change its original scoped native source.");
            return;
        }
        await ObservationPeriodDirectorySchema.Closes.InsertAsync(Transaction, identity, ct).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<ObservationPeriodCloseIdentity>> FindAsync(Uuid tenantId, Uuid programId,
        Uuid closeId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var values = await ObservationPeriodDirectorySchema.Closes.QueryAsync(tx,
            ObservationPeriodDirectorySchema.ByProgramClose.Query().WithPrefix(programId.ToString(), closeId.ToString())
                .Take(2), ct).ConfigureAwait(false);
        return values.Items;
    }

    public async ValueTask<Page<ObservationPeriodIdentity>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ObservationPeriodDirectorySchema.Periods.QueryAsync(tx,
            ObservationPeriodDirectorySchema.ByProgramOpened.Query().WithPrefix(programId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }
}
