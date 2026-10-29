using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Projections;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Controls.Services;

sealed class FitzControlDraftDirectoryV2(IKvClient client, ControlReader controls)
    : FitzKvProjectionStore(client, "kv://bdgrz/control-draft-directory-v3/projection", ProjectorName),
        IControlDraftDirectoryReader, IControlDraftDirectoryProjection
{
    public const string ProjectorName = "ControlDraftDirectoryV3";
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), ControlDraft.Area)), ct);

    public async ValueTask ApplyAsync(DomainEvent value, CancellationToken ct = default)
    {
        if (value is not IControlFact fact)
            return;
        ControlFactContent.ValidateScope(fact);
        if (fact.ChangeCount == 0)
            return;
        var header = fact.Header;
        var current = await ControlDraftDirectoryV2Schema.Controls.GetAsync(Transaction, header.ControlId, ct).ConfigureAwait(false);
        if (value is ControlDraftCreated)
        {
            if (current is not null)
            {
                if (current.Header != header)
                    throw new InvalidOperationException("A control identity cannot change its original creation.");
                return;
            }
        }
        else if (current is null || current.TenantId != header.TenantId || current.ProgramId != header.ProgramId ||
                 current.Identifier != header.Identifier || header.Revision < current.Header.Revision ||
                 header.Revision > current.Header.Revision + 1)
            throw new InvalidOperationException("A control mutation cannot project before its exact predecessor.");
        var identity = new ControlDirectoryIdentity(header.TenantId, header.ProgramId, header.ControlId, header.Identifier, header);
        if (!header.IsVisible)
        {
            if (current is not null)
                await ControlDraftDirectoryV2Schema.Controls.DeleteAsync(Transaction, current, ct).ConfigureAwait(false);
        }
        else if (current is null)
            await ControlDraftDirectoryV2Schema.Controls.InsertAsync(Transaction, identity, ct).ConfigureAwait(false);
        else
            await ControlDraftDirectoryV2Schema.Controls.ReplaceAsync(Transaction, current, identity, ct).ConfigureAwait(false);
        if (value is ControlDraftCreated or ControlDraftRevised or ControlSuccessorProposed or ControlProposalWithdrawn)
        {
            var row = new ControlRevisionIdentity(header.TenantId, header.ProgramId, header.ControlId,
                header.Identifier, header.Revision, header.Content, fact.Change);
            var existing = await ControlDraftDirectoryV2Schema.Revisions.GetAsync(Transaction,
                ControlDraftDirectoryV2Schema.RevisionKey(header.ControlId, header.Revision), ct).ConfigureAwait(false);
            if (existing is not null && existing != row)
                throw new InvalidOperationException("A control content revision cannot replace its immutable discovery identity.");
            if (existing is null)
                await ControlDraftDirectoryV2Schema.Revisions.InsertAsync(Transaction, row, ct).ConfigureAwait(false);
        }
    }

    public async ValueTask<ControlDraftView?> GetAsync(Uuid tenantId, Uuid controlId, CancellationToken ct = default)
    {
        ControlDirectoryIdentity? identity;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            identity = await ControlDraftDirectoryV2Schema.Controls.GetAsync(tx, controlId, ct).ConfigureAwait(false);
        if (identity is null)
            return null;
        return await ComposeAsync(tenantId, identity.ProgramId, identity, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<ControlDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        Page<ControlDirectoryIdentity> page;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            page = await ControlDraftDirectoryV2Schema.Controls.QueryAsync(tx,
                ControlDraftDirectoryV2Schema.ByProgramIdentifier.Query().WithPrefix(programId.ToString())
                    .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
        var values = new List<ControlDraftView>(page.Items.Count);
        foreach (var row in page.Items)
            values.Add(await ComposeAsync(tenantId, programId, row, ct).ConfigureAwait(false));
        return new Page<ControlDraftView>(values, page.NextCursor);
    }

    async ValueTask<ControlDraftView> ComposeAsync(Uuid tenantId, Uuid programId, ControlDirectoryIdentity identity, CancellationToken ct)
    {
        if (identity.TenantId != tenantId || identity.ProgramId != programId)
            throw new ImmutableContentIntegrityException("The control identity belongs to another tenant/program.");
        var snapshot = await controls.ReadAsync(tenantId, programId, identity.ControlId, ct).ConfigureAwait(false);
        if (!snapshot.IsVisible || snapshot.Draft is null || snapshot.Owner.Header != identity.Header)
            throw new ImmutableContentUnavailableException("The control identity projection changed or has not reached its native source.");
        return snapshot.Draft;
    }

    public async ValueTask<ControlDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid controlId, long revision,
        CancellationToken ct = default)
    {
        ControlRevisionIdentity? row;
        await using (var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            row = await ControlDraftDirectoryV2Schema.Revisions.GetAsync(tx,
                ControlDraftDirectoryV2Schema.RevisionKey(controlId, revision), ct).ConfigureAwait(false);
        if (row is null)
            return null;
        if (row.TenantId != tenantId || row.ControlId != controlId || row.Revision != revision)
            throw new ImmutableContentIntegrityException("The control revision identity has another exact scope.");
        return await controls.RevisionAsync(tenantId, row.ProgramId, controlId, revision, ct).ConfigureAwait(false);
    }
}
