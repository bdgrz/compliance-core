using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Projections;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Risks.Readers;

sealed class FitzRiskDraftDirectory(IKvClient client, IImmutableContentStore content, RiskDraftReader sources)
    : RiskDraftProjectionStore(client, content, sources, "kv://bdgrz/risk-draft-directory-v2/projection", ProjectorName),
        IRiskDraftDirectoryReader, IRiskDraftDirectoryProjection
{
    public const string ProjectorName = "RiskDraftDirectoryV2";
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName, EventStreamPattern.ForPattern(tenantId.ToString(), RiskDraft.Area)), ct);
    public async ValueTask ApplyAsync(DomainEvent value, CancellationToken ct = default)
    {
        var row = await IdentityAsync(value, ct).ConfigureAwait(false);
        var current = await RiskDraftDirectorySchema.Risks.GetAsync(Transaction, row.Header.RiskId, ct).ConfigureAwait(false);
        if (value is RiskDraftCreated ? current is not null || row.Header.Revision != 1 || row.NativeSequence != 1 :
            value is not RiskDraftRevised || current is null || current.Header.TenantId != row.Header.TenantId ||
            current.Header.ProgramId != row.Header.ProgramId || current.Header.Identifier != row.Header.Identifier ||
            current.Header.Revision + 1 != row.Header.Revision || current.NativeSequence + 1 != row.NativeSequence ||
            current.Source.AggregateVersion >= row.Source.AggregateVersion)
            throw new InvalidOperationException("A risk draft mutation cannot project before its exact predecessor.");
        await ValidateSourceAsync(value, ct).ConfigureAwait(false);
        if (current is null)
            await RiskDraftDirectorySchema.Risks.InsertAsync(Transaction, row, ct).ConfigureAwait(false);
        else
            await RiskDraftDirectorySchema.Risks.ReplaceAsync(Transaction, current, row, ct).ConfigureAwait(false);
        await RiskDraftDirectorySchema.Revisions.InsertAsync(Transaction, row, ct).ConfigureAwait(false);
    }
    public async ValueTask<RiskDraftView?> GetAsync(Uuid tenantId, Uuid riskId, CancellationToken ct = default)
    {
        RiskDraftDirectoryIdentity? row;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            row = await RiskDraftDirectorySchema.Risks.GetAsync(tx, riskId, ct).ConfigureAwait(false);
        if (row is not null && row.Header.RiskId != riskId)
            throw new ImmutableContentIntegrityException("The risk draft row has another requested risk identity.");
        return row is null ? null : RiskDraftFactContent.View(await ReadAsync(tenantId, row, ct).ConfigureAwait(false));
    }
    public async ValueTask<Page<RiskDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        Page<RiskDraftDirectoryIdentity> page;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            page = await RiskDraftDirectorySchema.Risks.QueryAsync(tx, RiskDraftDirectorySchema.ByProgramIdentifier.Query()
                .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
        var values = new List<RiskDraftView>(page.Items.Count);
        foreach (var row in page.Items)
        {
            if (row.Header.ProgramId != programId)
                throw new ImmutableContentIntegrityException("The risk draft list has another program scope.");
            values.Add(RiskDraftFactContent.View(await ReadAsync(tenantId, row, ct).ConfigureAwait(false)));
        }
        return new(values, page.NextCursor);
    }
    public async ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid riskId, long revision, CancellationToken ct = default)
    {
        RiskDraftDirectoryIdentity? row;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            row = await RiskDraftDirectorySchema.Revisions.GetAsync(tx, RiskDraftDirectorySchema.RevisionKey(riskId, revision), ct).ConfigureAwait(false);
        if (row is not null && (row.Header.RiskId != riskId || row.Header.Revision != revision))
            throw new ImmutableContentIntegrityException("The risk draft revision row has another requested risk identity or revision.");
        return row is null ? null : await ReadAsync(tenantId, row, ct).ConfigureAwait(false);
    }
}
