using System.Globalization;
using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.History;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Projections;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Programs.Readers;

sealed class FitzProgramDirectory(IKvClient client, IImmutableContentStore content)
    : FitzKvProjectionStore(client, "kv://bdgrz/program-directory/projection", "ProgramDirectory"),
        IProgramDirectoryReader, IProgramDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is IProgramFact native)
            ProgramHistory.ValidateHeader(native);
        switch (domainEvent)
        {
            case ProgramCreated created:
                var creation = await ProgramReader.ReadContentAsync(content, created, ct).ConfigureAwait(false);
                await ProgramDirectorySchema.Directory.InsertAsync(Transaction,
                        new ProgramView(created.TenantId, created.ProgramId, creation.Name,
                            "readiness", "type_i", 1, creation.Plan,
                            created.ActorMemberId, created.ActorDisplay, created.ChangedAt,
                            [
                                new ProgramStageView("readiness",
                                    "Approve the scoped readiness baseline and own its remaining gaps."),
                                new ProgramStageView("type_i",
                                    "Record the point-in-time examination outcome and Type II plan."),
                                new ProgramStageView("type_ii",
                                    "Close the operating period and record the examination outcome."),
                            ]), ct)
                    .ConfigureAwait(false);
                await ProgramDirectorySchema.Revisions.InsertAsync(Transaction,
                        new ProgramRevisionView(created.ProgramId, 1, creation.Name, creation.Plan,
                            created.ActorMemberId, created.ActorDisplay, created.ChangedAt), ct)
                    .ConfigureAwait(false);
                break;
            case ProgramRevised revised:
                var revision = await ProgramReader.ReadContentAsync(content, revised, ct).ConfigureAwait(false);
                var current = await ProgramDirectorySchema.Directory.GetAsync(Transaction,
                    revised.ProgramId, ct).ConfigureAwait(false);
                if (current is null)
                    throw new InvalidOperationException(
                        "A program revision cannot project before its creation.");
                await ProgramDirectorySchema.Directory.ReplaceAsync(Transaction, current,
                    current with
                    {
                        Name = revision.Name,
                        Revision = revised.Revision,
                        Plan = revision.Plan,
                        LastChangedByMemberId = revised.ActorMemberId,
                        LastChangedByDisplay = revised.ActorDisplay,
                        LastChangedAt = revised.ChangedAt,
                    }, ct).ConfigureAwait(false);
                await ProgramDirectorySchema.Revisions.InsertAsync(Transaction,
                    new ProgramRevisionView(revised.ProgramId, revised.Revision,
                        revision.Name, revision.Plan, revised.ActorMemberId,
                        revised.ActorDisplay, revised.ChangedAt)
                    {
                        CriteriaEditionId = current.CriteriaEditionId,
                    }, ct).ConfigureAwait(false);
                break;
            case ProgramCriteriaEditionSelected selected:
                if (selected.ChangeCount == 0)
                    break;
                _ = await ProgramReader.ReadContentAsync(content, selected, ct).ConfigureAwait(false);
                var beforeSelection = await ProgramDirectorySchema.Directory.GetAsync(Transaction,
                    selected.ProgramId, ct).ConfigureAwait(false);
                if (beforeSelection is null)
                    throw new InvalidOperationException(
                        "A program criteria selection cannot project before its creation.");
                await ProgramDirectorySchema.Directory.ReplaceAsync(Transaction, beforeSelection,
                    beforeSelection with
                    {
                        Revision = selected.Revision,
                        CriteriaEditionId = selected.EditionId,
                        LastChangedByMemberId = selected.ActorMemberId,
                        LastChangedByDisplay = selected.ActorDisplay,
                        LastChangedAt = selected.ChangedAt,
                    }, ct).ConfigureAwait(false);
                await ProgramDirectorySchema.Revisions.InsertAsync(Transaction,
                    new ProgramRevisionView(selected.ProgramId, selected.Revision,
                        beforeSelection.Name, beforeSelection.Plan, selected.ActorMemberId,
                        selected.ActorDisplay, selected.ChangedAt)
                    {
                        CriteriaEditionId = selected.EditionId,
                    }, ct).ConfigureAwait(false);
                break;
            case TypeIEntryDecisionRecorded recorded:
                // Only an approved Type I entry decision advances the stage; a target date,
                // scope approval, or deferral never does. Replay rewrites the same values.
                var entry = await content.ReadJsonAsync<TypeIEntryDecisionView>(recorded.TenantId.ToString(),
                    recorded.DecisionContent, ct).ConfigureAwait(false);
                if (entry.Outcome == ProgramReadiness.Defer)
                    break;
                var beforeEntry = await ProgramDirectorySchema.Directory.GetAsync(Transaction,
                    recorded.ProgramId, ct).ConfigureAwait(false);
                if (beforeEntry is null || beforeEntry.StageDecisionId == entry.DecisionId)
                    break;
                await ProgramDirectorySchema.Directory.ReplaceAsync(Transaction, beforeEntry,
                    beforeEntry with
                    {
                        Stage = "type_i",
                        NextStage = "type_ii",
                        StageDecisionId = entry.DecisionId,
                        StageEnteredBy = entry.DecidedBy,
                        StageEnteredAt = entry.DecidedAt,
                    }, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ProgramDirectorySchema.Directory.GetAsync(tx, programId, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ProgramDirectorySchema.Directory.QueryAsync(tx,
                ProgramDirectorySchema.ByName.Query().Take(Math.Clamp(limit, 1, 200)).After(cursor), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        if (await ProgramDirectorySchema.Directory.GetAsync(tx, programId, ct).ConfigureAwait(false) is null)
            return null;
        return await ProgramDirectorySchema.Revisions.QueryAsync(tx,
                ProgramDirectorySchema.RevisionsByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200)).After(cursor), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId,
        Uuid programId, long revision, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ProgramDirectorySchema.Revisions.GetAsync(tx,
                $"{programId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}", ct)
            .ConfigureAwait(false);
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("ProgramDirectory",
            EventStreamPattern.ForPattern(tenantId.ToString())), ct);
}
