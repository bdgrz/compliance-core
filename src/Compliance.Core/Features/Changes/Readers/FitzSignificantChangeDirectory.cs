using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Changes.Contracts;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Changes.Aggregates;
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.Changes.Events;
using Bdgrz.Compliance.Features.Changes.Projections;
using Bdgrz.Compliance.Features.Changes.Projectors;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.Changes.Readers;

sealed class FitzSignificantChangeDirectory(IKvClient client, SignificantChangeReader changes)
    : FitzKvProjectionStore(client, "kv://bdgrz/significant-change-directory/projection-v2", SignificantChangeDirectoryProjector.ProjectorName),
      ISignificantChangeDirectoryReader, ISignificantChangeDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent value, CancellationToken ct = default)
    {
        if (value is not ISignificantChangeFact fact)
            return;
        var h = fact.Header;
        if (h.TenantId != fact.TenantId || h.SignificantChangeId != fact.SubjectId || h.ProgramId == Uuid.Empty ||
            h.Revision != fact.Revision || fact.NativeSequence < 1 || fact.ChangeCount > 0 && fact.ActorMemberId != h.ActorMemberId)
            throw new InvalidOperationException("The significant-change directory source has another authority binding.");
        var next = new SignificantChangeDirectoryEntry(h.TenantId, h.ProgramId, h.SignificantChangeId, fact.NativeSequence, h);
        var summary = ToSummary(h);
        var current = await SignificantChangeDirectorySchema.Details.GetAsync(Transaction, h.SignificantChangeId, ct).ConfigureAwait(false);
        if (current is null)
        {
            if (value is not SignificantChangeRecorded || fact.NativeSequence != 1)
                throw new InvalidOperationException("A change must be created before its later directory facts.");
            await SignificantChangeDirectorySchema.Details.InsertAsync(Transaction, next, ct).ConfigureAwait(false);
            await SignificantChangeDirectorySchema.Summaries.InsertAsync(Transaction, summary, ct).ConfigureAwait(false);
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
        var priorSummary = await SignificantChangeDirectorySchema.Summaries.GetAsync(Transaction, h.SignificantChangeId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A projected change has no discovery summary.");
        await SignificantChangeDirectorySchema.Details.ReplaceAsync(Transaction, current, next, ct).ConfigureAwait(false);
        await SignificantChangeDirectorySchema.Summaries.ReplaceAsync(Transaction, priorSummary, summary, ct).ConfigureAwait(false);
    }
    public async ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        await base.LoadCheckpointAsync(new CheckpointIdentity(SignificantChangeDirectoryProjector.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), SignificantChange.Area)), ct).ConfigureAwait(false);
    public async ValueTask<SignificantChangeView?> GetAsync(Uuid tenantId, Uuid changeId, CancellationToken ct = default)
    {
        SignificantChangeDirectoryEntry? identity;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            identity = await SignificantChangeDirectorySchema.Details.GetAsync(tx, changeId, ct).ConfigureAwait(false);
        return identity is null ? null : (await changes.ReadAsync(tenantId, identity.ProgramId, changeId, ct).ConfigureAwait(false)).View;
    }
    public async ValueTask<Page<SignificantChangeSummaryView>> ListAsync(Uuid tenantId, Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await SignificantChangeDirectorySchema.Summaries.QueryAsync(tx, SignificantChangeDirectorySchema.ByProgramUpdatedAt.Query()
            .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }
    static SignificantChangeSummaryView ToSummary(SignificantChangeAuthorityHeader h) =>
        new(h.TenantId, h.ProgramId, h.SignificantChangeId, h.Title, h.Status, h.UpdatedAt);
}
