using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Applications.Projections;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Applications.Readers;

sealed class FitzApplicationBoundaryReferenceDirectory(IKvClient client, IImmutableContentStore content)
    : FitzKvProjectionStore(client,
            "kv://bdgrz/application-boundary-references-v3/projection",
            "ApplicationBoundaryReferencesV3"),
        IApplicationBoundaryReferenceDirectory, IApplicationBoundaryReferenceProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is not IBoundaryFact fact)
            throw new InvalidOperationException("A boundary reverse index requires one native source fact.");
        var mutation = await BoundaryFactContent.ReadChangeAsync(content, fact, ct).ConfigureAwait(false);
        // Review decisions do not change a boundary version's referenced records.
        foreach (var change in mutation.Changes)
            switch (change)
            {
                case BoundaryDraftCreatedContent created:
                    var initial = new BoundaryReferenceState(created.BoundaryId, created.ProgramId,
                        created.DraftVersionId, 1, ReferenceIds(created.Content), null, []);
                    await ApplicationBoundaryReferenceSchema.States.InsertAsync(Transaction,
                        initial, ct).ConfigureAwait(false);
                    await InsertDraftAsync(created.TenantId, initial, created.Content, ct)
                        .ConfigureAwait(false);
                    break;
                case BoundaryDraftRevisedContent revised:
                    var revising = await RequireStateAsync(revised.BoundaryId, ct)
                        .ConfigureAwait(false);
                    if (revising.DraftVersionId != revised.DraftVersionId ||
                        revising.DraftRevision + 1 != revised.Revision)
                        throw new InvalidOperationException("A boundary draft revision is out of order.");
                    await DeleteReferencesAsync(revising.BoundaryId, revised.DraftVersionId,
                        revising.DraftEntryIds, ct).ConfigureAwait(false);
                    var revisedState = revising with
                    {
                        DraftRevision = revised.Revision,
                        DraftEntryIds = ReferenceIds(revised.Content),
                    };
                    await ApplicationBoundaryReferenceSchema.States.ReplaceAsync(Transaction,
                        revising, revisedState, ct).ConfigureAwait(false);
                    await InsertDraftAsync(revised.TenantId, revisedState, revised.Content, ct)
                        .ConfigureAwait(false);
                    break;
                case BoundaryDraftDiscardedContent discarded:
                    var discarding = await RequireStateAsync(discarded.BoundaryId, ct)
                        .ConfigureAwait(false);
                    if (discarding.DraftVersionId != discarded.DraftVersionId ||
                        discarding.DraftRevision != discarded.Revision)
                        throw new InvalidOperationException("A boundary draft discard is out of order.");
                    await DeleteReferencesAsync(discarding.BoundaryId, discarded.DraftVersionId,
                        discarding.DraftEntryIds, ct).ConfigureAwait(false);
                    await ApplicationBoundaryReferenceSchema.States.ReplaceAsync(Transaction,
                        discarding, discarding with
                        {
                            DraftVersionId = null,
                            DraftRevision = 0,
                            DraftEntryIds = [],
                        }, ct).ConfigureAwait(false);
                    break;
                case BoundaryApprovedContent approved:
                    var approving = await RequireStateAsync(approved.BoundaryId, ct)
                        .ConfigureAwait(false);
                    if (approving.DraftVersionId != approved.DraftVersionId ||
                        approving.DraftRevision != approved.Revision)
                        throw new InvalidOperationException("A boundary approval is out of order.");
                    if (approving.ApprovedVersionId is { } previousVersion)
                        await ChangeStatusAsync(approving.BoundaryId, previousVersion,
                                approving.ApprovedEntryIds, "historical", null, ct)
                            .ConfigureAwait(false);
                    await ChangeStatusAsync(approving.BoundaryId, approved.DraftVersionId,
                            approving.DraftEntryIds, "approved", approved.EffectiveFrom, ct)
                        .ConfigureAwait(false);
                    await ApplicationBoundaryReferenceSchema.States.ReplaceAsync(Transaction,
                        approving, approving with
                        {
                            DraftVersionId = null,
                            DraftRevision = 0,
                            DraftEntryIds = [],
                            ApprovedVersionId = approved.DraftVersionId,
                            ApprovedEntryIds = approving.DraftEntryIds,
                        }, ct).ConfigureAwait(false);
                    break;
                case BoundarySuccessorProposedContent proposed:
                    var predecessor = await RequireStateAsync(proposed.BoundaryId, ct)
                        .ConfigureAwait(false);
                    if (predecessor.ApprovedVersionId != proposed.PredecessorVersionId ||
                        predecessor.DraftVersionId is not null)
                        throw new InvalidOperationException("A boundary successor is out of order.");
                    var successor = predecessor with
                    {
                        DraftVersionId = proposed.DraftVersionId,
                        DraftRevision = 1,
                        DraftEntryIds = ReferenceIds(proposed.Content),
                    };
                    await ApplicationBoundaryReferenceSchema.States.ReplaceAsync(Transaction,
                        predecessor, successor, ct).ConfigureAwait(false);
                    await InsertDraftAsync(proposed.TenantId, successor, proposed.Content, ct)
                        .ConfigureAwait(false);
                    break;
            }
    }

    static Uuid[] ReferenceIds(BoundaryContent content) =>
        [.. content.Entries.Where(IsIndexed).Select(static entry => entry.EntryId)];

    // V2 also indexes technology components and information assets so inventory reads can show
    // which boundaries include them.
    static bool IsIndexed(BoundaryScopeEntry entry) =>
        !entry.Unresolved && entry.GovernedRecordId is not null &&
        entry.SubjectType is "application" or "system_instance" or "component" or "information";

    async ValueTask<BoundaryReferenceState> RequireStateAsync(Uuid boundaryId,
        CancellationToken ct) =>
        await ApplicationBoundaryReferenceSchema.States.GetAsync(Transaction, boundaryId, ct)
            .ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A boundary reference change cannot project before its creation.");

    async ValueTask InsertDraftAsync(Uuid tenantId, BoundaryReferenceState state,
        BoundaryContent content, CancellationToken ct)
    {
        foreach (var entry in content.Entries.Where(IsIndexed))
            await ApplicationBoundaryReferenceSchema.References.InsertAsync(Transaction,
                new ApplicationBoundaryReferenceView(tenantId, entry.SubjectType,
                    entry.GovernedRecordId!.Value, state.BoundaryId, state.ProgramId,
                    state.DraftVersionId!.Value, entry.EntryId, state.DraftRevision,
                    "draft", null, entry.Kind, entry.Subject, entry.OwnerReference,
                    entry.Rationale), ct).ConfigureAwait(false);
    }

    async ValueTask DeleteReferencesAsync(Uuid boundaryId, Uuid versionId,
        IReadOnlyList<Uuid> entryIds, CancellationToken ct)
    {
        foreach (var entryId in entryIds)
            await ApplicationBoundaryReferenceSchema.References.DeleteAsync(Transaction,
                (boundaryId, versionId, entryId), ct).ConfigureAwait(false);
    }

    async ValueTask ChangeStatusAsync(Uuid boundaryId, Uuid versionId,
        IReadOnlyList<Uuid> entryIds, string status, DateOnly? effectiveFrom,
        CancellationToken ct)
    {
        foreach (var entryId in entryIds)
        {
            var current = await ApplicationBoundaryReferenceSchema.References.GetAsync(Transaction,
                              (boundaryId, versionId, entryId), ct).ConfigureAwait(false) ??
                          throw new InvalidOperationException("A boundary reference row is missing.");
            await ApplicationBoundaryReferenceSchema.References.ReplaceAsync(Transaction,
                current, current with
                {
                    Status = status,
                    EffectiveFrom = effectiveFrom ?? current.EffectiveFrom,
                }, ct).ConfigureAwait(false);
        }
    }

    public async ValueTask<Page<ApplicationBoundaryReferenceView>> ListAsync(Uuid tenantId,
        string subjectType, Uuid recordId, int limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ApplicationBoundaryReferenceSchema.References.QueryAsync(tx,
            ApplicationBoundaryReferenceSchema.ByRecord.Query()
                .WithPrefix(subjectType, recordId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("ApplicationBoundaryReferencesV3",
            EventStreamPattern.ForPattern(tenantId.ToString(), "boundaries")), ct);
}
