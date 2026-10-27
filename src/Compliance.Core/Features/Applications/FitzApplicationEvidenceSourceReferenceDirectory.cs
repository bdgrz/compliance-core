using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class FitzApplicationEvidenceSourceReferenceDirectory(IKvClient client)
    : FitzKvProjectionStore(client,
            "kv://bdgrz/application-evidence-source-references-v1/projection", ProjectorName),
        IApplicationEvidenceSourceReferenceDirectory,
        IApplicationEvidenceSourceReferenceProjection
{
    public const string ProjectorName = "ApplicationEvidenceSourceReferencesV1";
    static readonly HashSet<string> SupportedRecordTypes = new(StringComparer.Ordinal)
    {
        "control", "control_version", "control_occurrence", "control_evaluation", "period",
    };
    Uuid? _batchTenantId;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context,
        CancellationToken ct = default) => BeginEvidenceBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context,
        CancellationToken ct) => BeginEvidenceBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginEvidenceBatchAsync(ProjectionBatchContext context,
        CancellationToken ct)
    {
        if (context.Identity.Pattern.Area != "evidence-support" ||
            context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) ||
            tenantId == Uuid.Empty)
            throw new InvalidOperationException(
                "An application evidence-source batch requires its tenant's evidence-support area.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is not EvidenceSupportLinked linked)
            throw new InvalidOperationException(
                "The domain event is not supported by the application evidence-source projection.");
        EnsureValid(linked);
        if (linked.SupportedRecordType is "control" or "control_version")
        {
            await ApplicationEvidenceSourceReferenceSchema.References.InsertAsync(Transaction,
                new ApplicationEvidenceSourceReferenceView(linked.TenantId, linked.ProgramId,
                    linked.SubjectId, linked.SupportedRecordId, linked.SupportedRecordType,
                    linked.SupportedRecordRevision, linked.ArtifactId, linked.ArtifactRevision,
                    linked.PeriodStart, linked.PeriodEnd, linked.Purpose!), ct)
                .ConfigureAwait(false);
            return;
        }
        await IncrementUnresolvedCoverageAsync(linked.TenantId, linked.ProgramId, ct)
            .ConfigureAwait(false);
    }

    void EnsureValid(EvidenceSupportLinked linked)
    {
        if (_batchTenantId != linked.TenantId)
            throw new InvalidOperationException(
                "An evidence-support event must belong to its tenant projection batch.");
        if (linked.SubjectId == Uuid.Empty || linked.ProgramId == Uuid.Empty ||
            linked.Revision != 1 || linked.ArtifactId == Uuid.Empty ||
            linked.ArtifactRevision <= 0 || !IsSha256(linked.ArtifactContentSha256) ||
            linked.SupportedRecordType is not { } type || !SupportedRecordTypes.Contains(type) ||
            linked.SupportedRecordId == Uuid.Empty || linked.SupportedRecordRevision <= 0 ||
            linked.PeriodStart == default || linked.PeriodEnd == default ||
            linked.PeriodStart > linked.PeriodEnd ||
            string.IsNullOrWhiteSpace(linked.Purpose) || linked.Purpose != linked.Purpose.Trim() ||
            linked.Purpose.Length > 2000 ||
            string.IsNullOrWhiteSpace(linked.SufficiencyRationale) ||
            linked.SufficiencyRationale != linked.SufficiencyRationale.Trim() ||
            linked.SufficiencyRationale.Length > 2000 || linked.ActorMemberId is not { } actorId ||
            actorId == Uuid.Empty || string.IsNullOrWhiteSpace(linked.ActorDisplay) ||
            linked.ActorDisplay != linked.ActorDisplay.Trim() || linked.ActorDisplay.Length > 200 ||
            linked.OccurredAt == default)
            throw new InvalidOperationException("The evidence support link event is invalid.");
    }

    async ValueTask IncrementUnresolvedCoverageAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct)
    {
        var current = await ApplicationEvidenceSourceReferenceSchema.Coverage.GetAsync(
            Transaction, programId, ct).ConfigureAwait(false);
        var nextCount = checked((current?.UnresolvedReferenceCount ?? 0) + 1);
        if (current is null)
            await ApplicationEvidenceSourceReferenceSchema.Coverage.InsertAsync(Transaction,
                new ApplicationEvidenceSourceReferenceCoverage(tenantId, programId, nextCount), ct)
                .ConfigureAwait(false);
        else
            await ApplicationEvidenceSourceReferenceSchema.Coverage.ReplaceAsync(Transaction,
                current, current with { UnresolvedReferenceCount = nextCount }, ct)
                .ConfigureAwait(false);
    }

    static bool IsSha256(string? value) => value is { Length: 64 } &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    public async ValueTask<Page<ApplicationEvidenceSourceReferenceView>> ListByControlAsync(
        Uuid tenantId, Uuid controlId, int limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ApplicationEvidenceSourceReferenceSchema.References.QueryAsync(tx,
            ApplicationEvidenceSourceReferenceSchema.ByControl.Query()
                .WithPrefix(controlId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }

    public async ValueTask<bool> HasUnresolvedReferencesAsync(Uuid tenantId,
        IReadOnlyCollection<Uuid> programIds, CancellationToken ct = default)
    {
        if (programIds.Count == 0)
            return false;
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        foreach (var programId in programIds.Where(static id => id != Uuid.Empty).Distinct())
        {
            var coverage = await ApplicationEvidenceSourceReferenceSchema.Coverage.GetAsync(tx,
                programId, ct).ConfigureAwait(false);
            if (coverage is { UnresolvedReferenceCount: > 0 } &&
                coverage.TenantId == tenantId && coverage.ProgramId == programId)
                return true;
        }
        return false;
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "evidence-support")), ct);
}
