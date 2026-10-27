using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Applications.Projections;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.Readers;

sealed class FitzApplicationControlDraftReferenceDirectory(IKvClient client, IImmutableContentStore content)
    : FitzKvProjectionStore(client,
            "kv://bdgrz/application-control-draft-references-v1/projection",
            "ApplicationControlDraftReferencesV1"),
        IApplicationControlDraftReferenceDirectory, IApplicationControlDraftReferenceProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is IControlFact { ChangeCount: 0 })
            return;
        switch (domainEvent)
        {
            case ControlDraftCreated created:
                var initialContent = await ControlFactContent.ReadDraftAsync(content, created, ct)
                    .ConfigureAwait(false);
                var initial = new ControlDraftReferenceState(created.TenantId, created.ControlId,
                    created.ProgramId, created.Identifier, 1, ReferenceIds(initialContent));
                await ApplicationControlDraftReferenceSchema.States.InsertAsync(Transaction,
                    initial, ct).ConfigureAwait(false);
                await InsertAsync(created.TenantId, initial, initialContent, ct)
                    .ConfigureAwait(false);
                break;
            case ControlDraftRevised revised:
                await ReviseAsync(revised.TenantId, revised.ProgramId, revised.ControlId,
                    revised.Header.Revision, revised, ct).ConfigureAwait(false);
                break;
            case ControlSuccessorProposed proposed:
                await ReviseAsync(proposed.TenantId, proposed.ProgramId, proposed.ControlId,
                    proposed.Header.Revision, proposed, ct).ConfigureAwait(false);
                break;
            case ControlProposalWithdrawn withdrawn:
                await ReviseAsync(withdrawn.TenantId, withdrawn.ProgramId, withdrawn.ControlId,
                    withdrawn.Header.Revision, withdrawn, ct).ConfigureAwait(false);
                break;
            case ControlDraftDiscarded discarded:
                var discarding = await RequireStateAsync(discarded.ControlId, ct)
                    .ConfigureAwait(false);
                if (discarding.TenantId != discarded.TenantId ||
                    discarding.ProgramId != discarded.ProgramId ||
                    discarding.Revision != discarded.Revision)
                    throw new InvalidOperationException(
                        "A control draft reference discard is out of order.");
                await DeleteReferencesAsync(discarding.ControlId, discarding.EntryIds, ct)
                    .ConfigureAwait(false);
                await ApplicationControlDraftReferenceSchema.States.DeleteAsync(Transaction,
                    discarded.ControlId, ct).ConfigureAwait(false);
                break;
        }
    }

    async ValueTask ReviseAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        long revision, IControlFact fact, CancellationToken ct)
    {
        var revising = await RequireStateAsync(controlId, ct).ConfigureAwait(false);
        if (revising.TenantId != tenantId || revising.ProgramId != programId ||
            revising.Revision + 1 != revision)
            throw new InvalidOperationException("A control draft reference revision is out of order.");
        var revisedContent = await ControlFactContent.ReadDraftAsync(content, fact, ct).ConfigureAwait(false);
        await DeleteReferencesAsync(controlId, revising.EntryIds, ct).ConfigureAwait(false);
        var next = revising with { Revision = revision, EntryIds = ReferenceIds(revisedContent) };
        await ApplicationControlDraftReferenceSchema.States.ReplaceAsync(Transaction,
            revising, next, ct).ConfigureAwait(false);
        await InsertAsync(tenantId, next, revisedContent, ct).ConfigureAwait(false);
    }

    static Uuid[] ReferenceIds(ControlDraftContent content) =>
        [.. (content.Applicability ?? []).Where(IsIndexed).Select(static reference => reference.EntryId)];

    static bool IsIndexed(ControlApplicabilityReference? reference) =>
        reference is { Unresolved: false, GovernedRecordId: { } recordId } &&
        recordId != Uuid.Empty && reference.SubjectType is "application" or "system_instance";

    async ValueTask<ControlDraftReferenceState> RequireStateAsync(Uuid controlId,
        CancellationToken ct) =>
        await ApplicationControlDraftReferenceSchema.States.GetAsync(Transaction, controlId, ct)
            .ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A control draft reference change cannot project before its creation.");

    async ValueTask InsertAsync(Uuid tenantId, ControlDraftReferenceState state,
        ControlDraftContent content, CancellationToken ct)
    {
        foreach (var reference in (content.Applicability ?? []).Where(IsIndexed))
            await ApplicationControlDraftReferenceSchema.References.InsertAsync(Transaction,
                new ApplicationControlDraftReferenceView(tenantId, reference.SubjectType,
                    reference.GovernedRecordId!.Value, state.ProgramId, state.ControlId,
                    state.Identifier, state.Revision, reference.EntryId, reference.Subject,
                    reference.Rationale), ct).ConfigureAwait(false);
    }

    async ValueTask DeleteReferencesAsync(Uuid controlId, IReadOnlyList<Uuid> entryIds,
        CancellationToken ct)
    {
        foreach (var entryId in entryIds)
            await ApplicationControlDraftReferenceSchema.References.DeleteAsync(Transaction,
                (controlId, entryId), ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<ApplicationControlDraftReferenceView>> ListAsync(Uuid tenantId,
        string subjectType, Uuid recordId, int limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ApplicationControlDraftReferenceSchema.References.QueryAsync(tx,
            ApplicationControlDraftReferenceSchema.ByRecord.Query()
                .WithPrefix(subjectType, recordId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("ApplicationControlDraftReferencesV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "controls")), ct);
}
