using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

sealed class FitzComplianceIncidentDirectory(IKvClient client, ComplianceIncidentReader changes)
    : FitzKvProjectionStore(client, "kv://bdgrz/compliance-incident-directory/projection-v2", ComplianceIncidentDirectoryProjector.ProjectorName),
      IComplianceIncidentDirectoryReader, IComplianceIncidentDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent value, CancellationToken ct = default)
    {
        if (value is not IComplianceIncidentFact fact)
            return;
        var h = fact.Header;
        if (h.TenantId != fact.TenantId || h.IncidentId != fact.SubjectId || h.ProgramId == Uuid.Empty ||
            h.Revision != fact.Revision || fact.NativeSequence < 1 || fact.ChangeCount > 0 && fact.ActorMemberId != h.ActorMemberId)
            throw new InvalidOperationException("The compliance-incident directory source has another authority binding.");
        var next = new ComplianceIncidentDirectoryEntry(h.TenantId, h.ProgramId, h.IncidentId, fact.NativeSequence, h);
        var summary = ToSummary(h);
        var current = await ComplianceIncidentDirectorySchema.Details.GetAsync(Transaction, h.IncidentId, ct).ConfigureAwait(false);
        if (current is null)
        {
            if (value is not ComplianceIncidentRecorded || fact.NativeSequence != 1)
                throw new InvalidOperationException("A change must be created before its later directory facts.");
            await ComplianceIncidentDirectorySchema.Details.InsertAsync(Transaction, next, ct).ConfigureAwait(false);
            await ComplianceIncidentDirectorySchema.Summaries.InsertAsync(Transaction, summary, ct).ConfigureAwait(false);
            return;
        }
        if (current.TenantId != h.TenantId || current.ProgramId != h.ProgramId)
            throw new InvalidOperationException("A change cannot change its directory scope.");
        if (fact.NativeSequence < current.NativeSequence)
            return;
        if (fact.NativeSequence == current.NativeSequence)
        {
            if (current != next)
                throw new InvalidOperationException("The change directory has conflicting same-sequence authority.");
            return;
        }
        if (fact.NativeSequence != current.NativeSequence + 1)
            throw new InvalidOperationException("The change directory skipped a native source fact.");
        var priorSummary = await ComplianceIncidentDirectorySchema.Summaries.GetAsync(Transaction, h.IncidentId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A projected change has no discovery summary.");
        await ComplianceIncidentDirectorySchema.Details.ReplaceAsync(Transaction, current, next, ct).ConfigureAwait(false);
        await ComplianceIncidentDirectorySchema.Summaries.ReplaceAsync(Transaction, priorSummary, summary, ct).ConfigureAwait(false);
    }
    public async ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        await base.LoadCheckpointAsync(new CheckpointIdentity(ComplianceIncidentDirectoryProjector.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), ComplianceIncident.Area)), ct).ConfigureAwait(false);
    public async ValueTask<ComplianceIncidentView?> GetAsync(Uuid tenantId, Uuid changeId, CancellationToken ct = default)
    {
        ComplianceIncidentDirectoryEntry? identity;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            identity = await ComplianceIncidentDirectorySchema.Details.GetAsync(tx, changeId, ct).ConfigureAwait(false);
        return identity is null ? null : (await changes.ReadAsync(tenantId, identity.ProgramId, changeId, ct).ConfigureAwait(false)).View;
    }
    public async ValueTask<Page<ComplianceIncidentSummaryView>> ListAsync(Uuid tenantId, Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ComplianceIncidentDirectorySchema.Summaries.QueryAsync(tx, ComplianceIncidentDirectorySchema.ByProgramUpdatedAt.Query()
            .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }
    static ComplianceIncidentSummaryView ToSummary(ComplianceIncidentAuthorityHeader h) =>
        new(h.TenantId, h.ProgramId, h.IncidentId, h.Title, h.Status, h.UpdatedAt);
}
