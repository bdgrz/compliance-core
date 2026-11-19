using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Projections;
using Bdgrz.Compliance.Features.Criteria.Projectors;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Readers;

/// <summary>A checkpointed discovery index; never an authority for write decisions.</summary>
sealed partial class FitzRuntimeCriteriaCatalog : FitzKvProjectionStore, IRuntimeCriteriaCatalogProjection,
        IRuntimeCriteriaCatalogReader
{
    public FitzRuntimeCriteriaCatalog(IKvClient client)
        : base(client, "kv://bdgrz/runtime-criteria-catalog-v1/projection", RuntimeCriteriaCatalogProjector.WorkloadName)
    {
    }
    CheckpointIdentity? _identity;
    string _generation = "live";

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context,
        CancellationToken ct = default) => BeginCatalogAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context,
        CancellationToken ct) => BeginCatalogAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginCatalogAsync(ProjectionBatchContext context, CancellationToken ct)
    {
        if (context.Identity.Pattern.Realm != "bdgrz" || context.Identity.Pattern.Area is not null ||
            context.Identity.Pattern.Resource is not null)
            throw new InvalidOperationException("Criteria indexing requires the platform source stream.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _identity = context.Identity;
        _generation = context.Identity.RebuildId is null ? "live" : "rebuild:" + context.Identity.RebuildId;
        return batch;
    }

    public async ValueTask ApplyAsync(DomainEventRecord source, IProjectorContext context, CancellationToken ct)
    {
        if (context.Identity != _identity || source.Stream.Realm != "bdgrz")
            throw new InvalidOperationException("Criteria projection source does not match its checkpoint.");
        var ev = source.Event;
        var editionId = ev switch
        {
            CriteriaEditionDraftRequested x => x.EditionId,
            CriteriaEditionOperationAttemptFenced x => x.EditionId,
            CriteriaInitialLabelRequested x => x.EditionId,
            CriteriaInitialLabelRefused x => x.EditionId,
            CriteriaEditionDraftActivated x => x.EditionId,
            CriteriaEditionDrafted x => x.EditionId,
            CriteriaEditionDraftRequestCancelled x => x.EditionId,
            CriteriaEditionRenameRequested x => x.EditionId,
            CriteriaEditionDraftRenamed x => x.EditionId,
            CriteriaEditionRenameCompleted x => x.EditionId,
            CriteriaEditionRenameRefused x => x.EditionId,
            CriteriaEditionRenameCancelled x => x.EditionId,
            CriteriaPublicationStarted x => x.EditionId,
            CriteriaPublicationAborted x => x.EditionId,
            CriteriaPublicationAbortCleanupCompleted x => x.EditionId,
            CriteriaEditionPublished x => x.EditionId,
            CriteriaEditionDraftAbandoned x => x.EditionId,
            CriterionChangeProposed x => x.Intent.EditionId,
            CriterionSeedProposed x => x.Intent.EditionId,
            CriterionChanged x => x.EditionId,
            CriterionChangeRefused x => x.EditionId,
            CriterionSummaryCorrected x => x.Intent.EditionId,
            CriterionSummaryUnchanged x => x.Intent.EditionId,
            CriterionSummaryRefused x => x.Intent.EditionId,
            CriteriaEditionValueOperationClaimed x => x.Intent.EditionId,
            CriteriaEditionValueProposed x => x.Intent.EditionId,
            CriteriaEditionValueAccepted x => x.EditionId,
            CriteriaEditionValueRefused x => x.EditionId,
            CriteriaSeedMetadataRecorded x => x.EditionId,
            CriteriaSeedDisclosureRecorded x => x.EditionId,
            CriteriaSeedImportCompleted x => x.EditionId,
            _ => Uuid.Empty,
        };
        if (editionId == Uuid.Empty)
            return;
        if (source.Stream.Area != $"criteria-{editionId}" || ev.Metadata.IsAudit || ev.Metadata.EventId == Uuid.Empty)
            throw new InvalidOperationException("A criteria projection fact has an invalid source.");
        var clock = await AdvanceClockAsync(source, ct).ConfigureAwait(false);
        var editionOwner = new CriteriaEdition(editionId);
        var editionKey = new RuntimeCriteriaProjectionSchema.EditionKey(_generation, editionId);
        var edition = await RuntimeCriteriaProjectionSchema.Editions.GetAsync(Transaction, editionKey, ct)
            .ConfigureAwait(false);
        var isActiveDraft = ev switch
        {
            CriteriaEditionDrafted or CriteriaEditionRenameCompleted or CriteriaEditionRenameRefused or
                CriteriaEditionRenameCancelled or CriteriaPublicationAbortCleanupCompleted => true,
            CriteriaEditionDraftRequested or CriteriaEditionDraftActivated or CriteriaEditionRenameRequested or
                CriteriaEditionDraftRenamed or CriteriaPublicationStarted or CriteriaPublicationAborted or
                CriteriaEditionDraftRequestCancelled or CriteriaEditionDraftAbandoned or CriteriaEditionPublished => false,
            _ => edition?.IsActiveDraft ?? false,
        };
        if (source.Stream == editionOwner.Stream)
        {
            if (ev.Metadata.AggregateId != editionOwner.Id)
                throw new InvalidOperationException("An edition source fact must match its owner.");
            await RuntimeCriteriaProjectionSchema.EditionFacts.InsertAsync(Transaction,
                new RuntimeCriteriaEditionFactIndexRow(_generation, editionId, ev.Metadata.EventId,
                    source.ResourceOffset, clock.Ordinal,
                    ev is CriteriaEditionPublished or CriteriaEditionDraftAbandoned, isActiveDraft), ct).ConfigureAwait(false);
        }
        if (ev is CriteriaEditionDraftRequested requested)
        {
            if (requested.Framework != "tsc" || source.Stream != new CriteriaEdition(editionId).Stream ||
                ev.Metadata.AggregateId != new CriteriaEdition(editionId).Id || edition is not null)
                throw new InvalidOperationException("A criteria draft requires its unique edition stream.");
            edition = new RuntimeCriteriaEditionIndexRow(_generation, editionId, requested.DraftGeneration,
                "pending", requested.Label, null, null, 0, DraftRequestEventId: ev.Metadata.EventId,
                Framework: requested.Framework, SeedVersion: requested.SeedVersion, SeedFingerprint: requested.SeedFingerprint, Revision: 1);
            await RuntimeCriteriaProjectionSchema.Editions.InsertAsync(Transaction, edition, ct).ConfigureAwait(false);
            return;
        }
        if (edition is null && ev is CriteriaEditionOperationAttemptFenced)
            return;
        if (edition is null)
            throw new InvalidOperationException("A criteria fact precedes its source edition draft.");
        var previous = edition;
        if (source.Stream == editionOwner.Stream)
            edition = edition with { IsActiveDraft = isActiveDraft };
        switch (ev)
        {
            case CriteriaEditionValueOperationClaimed claimed:
                await ApplyValueClaimAsync(source, edition, claimed, ct).ConfigureAwait(false);
                break;
            case CriteriaEditionValueProposed proposedValue:
                edition = await ApplyValueProposalAsync(source, edition, proposedValue, ct).ConfigureAwait(false);
                break;
            case CriteriaEditionValueAccepted acceptedValue:
                edition = await ApplyValueDecisionAsync(source, edition, acceptedValue.OperationId,
                    acceptedValue.ExecutionGeneration, acceptedValue.ProposalEventId, acceptedValue.Evidence,
                    true, null, ct).ConfigureAwait(false);
                break;
            case CriteriaEditionValueRefused refusedValue:
                edition = await ApplyValueDecisionAsync(source, edition, refusedValue.OperationId,
                    refusedValue.ExecutionGeneration, refusedValue.ProposalEventId, refusedValue.Evidence,
                    false, refusedValue.Reason, ct).ConfigureAwait(false);
                break;
            case CriteriaEditionDrafted drafted:
                RequireEditionSource(source, editionId);
                if (drafted.DraftGeneration != edition.DraftGeneration || drafted.Framework != edition.Framework ||
                    drafted.Framework != "tsc" || edition.Status != "pending")
                    throw new InvalidOperationException("A drafted edition must match its request.");
                edition = edition with { Status = "draft", Label = drafted.Label };
                break;
            case CriteriaEditionDraftRenamed renamed:
                RequireEditionSource(source, editionId);
                if (edition.Status != "draft")
                    throw new InvalidOperationException("Only a draft may be renamed.");
                edition = edition with { Label = renamed.NewLabel };
                break;
            case CriteriaEditionPublished published:
                RequireEditionSource(source, editionId);
                if (edition.Status != "draft" || published.DraftGeneration != edition.DraftGeneration)
                    throw new InvalidOperationException("Publication must match its source draft.");
                edition = edition with
                {
                    Status = "published",
                    Label = published.Label,
                    PublicationEventId = ev.Metadata.EventId,
                    PublishedAt = ev.Metadata.OccurredOn,
                    PublicationOrdinal = clock.Ordinal,
                    PublicationResourceOffset = source.ResourceOffset
                };
                break;
            case CriteriaEditionDraftAbandoned abandoned:
                RequireEditionSource(source, editionId);
                if (edition.Status != "draft" || abandoned.DraftGeneration != edition.DraftGeneration)
                    throw new InvalidOperationException("Abandonment must match its source draft.");
                edition = edition with
                {
                    Status = "abandoned",
                    AbandonmentEventId = ev.Metadata.EventId,
                    AbandonmentResourceOffset = source.ResourceOffset
                };
                break;
            case CriteriaEditionDraftRequestCancelled:
                RequireEditionSource(source, editionId);
                if (edition.Status != "pending")
                    throw new InvalidOperationException("Only an unfinished draft request may be cancelled.");
                edition = edition with { Status = "cancelled" };
                break;
            case CriterionChangeProposed or CriterionSeedProposed:
                var (intent, executionGeneration, incarnationId) = ev switch
                {
                    CriterionChangeProposed human => (human.Intent, human.ExecutionGeneration, human.IncarnationId),
                    CriterionSeedProposed seed when edition.DraftRequestEventId == seed.EditionRequestEventId &&
                        edition.SeedVersion == seed.SeedVersion && edition.SeedFingerprint == seed.SeedFingerprint &&
                        PlatformCriteriaSeedDefinition.MatchesProposal(seed) =>
                        (seed.Intent, seed.ExecutionGeneration, seed.IncarnationId),
                    _ => throw new InvalidOperationException("A seed proposal must bind its source edition request."),
                };
                RequireEntrySource(source, editionId, intent.Identifier);
                if (intent.DraftGeneration != edition.DraftGeneration)
                    throw new InvalidOperationException("A criterion proposal belongs to another draft.");
                var key = new RuntimeCriteriaProjectionSchema.EntryKey(_generation, editionId, intent.Identifier);
                var row = await RuntimeCriteriaProjectionSchema.Entries.GetAsync(Transaction, key, ct).ConfigureAwait(false);
                var initial = row ?? new RuntimeCriteriaEntryIndexRow(_generation, editionId, intent.Identifier,
                    edition.DraftGeneration, false, null, null, null, null, null, null, null);
                var beforeCut = edition.Status != "published";
                var pending = new RuntimeCriteriaPendingIndex(intent.OperationId, executionGeneration,
                    ev.Metadata.EventId, beforeCut, intent.Action, incarnationId, intent.Kind,
                    intent.Category, intent.ParentIdentifier, intent.ParentIncarnationId, intent.Summary);
                var proposedRow = RuntimeCriteriaProjectionRules.Propose(initial, pending);
                if (row is null)
                    await RuntimeCriteriaProjectionSchema.Entries.InsertAsync(Transaction, proposedRow, ct).ConfigureAwait(false);
                else
                    await RuntimeCriteriaProjectionSchema.Entries.ReplaceAsync(Transaction, row, proposedRow, ct).ConfigureAwait(false);
                if (beforeCut)
                    edition = edition with { PendingBeforeCut = checked(edition.PendingBeforeCut + 1) };
                break;
            case CriterionChanged changed:
                edition = await SettleAsync(source, edition, changed.Identifier, changed.OperationId,
                    changed.ExecutionGeneration, changed.ProposalEventId, true, ct).ConfigureAwait(false);
                break;
            case CriterionChangeRefused refused:
                edition = await SettleAsync(source, edition, refused.Identifier, refused.OperationId,
                    refused.ExecutionGeneration, refused.ProposalEventId, false, ct).ConfigureAwait(false);
                break;
            case CriterionSummaryCorrected corrected:
                RequireEntrySource(source, editionId, corrected.Intent.Identifier);
                var summaryKey = new RuntimeCriteriaProjectionSchema.EntryKey(_generation, editionId, corrected.Intent.Identifier);
                var summaryRow = await RuntimeCriteriaProjectionSchema.Entries.GetAsync(Transaction, summaryKey, ct).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("A summary correction needs its entry.");
                if (!summaryRow.Included || summaryRow.Pending is not null)
                    throw new InvalidOperationException("A summary correction requires a settled entry.");
                await RuntimeCriteriaProjectionSchema.Entries.ReplaceAsync(Transaction, summaryRow,
                    summaryRow with { Summary = corrected.Intent.Summary }, ct).ConfigureAwait(false);
                break;
            case CriterionSummaryUnchanged unchanged:
                RequireEntrySource(source, editionId, unchanged.Intent.Identifier);
                break;
            case CriterionSummaryRefused refusedSummary:
                RequireEntrySource(source, editionId, refusedSummary.Intent.Identifier);
                break;
            case CriteriaSeedMetadataRecorded metadata:
                if (source.Stream != CriteriaSeedMetadata.AddressOf(editionId) ||
                    ev.Metadata.AggregateId != CriteriaSeedMetadata.IdentityOf(editionId) ||
                    metadata.Framework != edition.Framework || edition.DraftRequestEventId != metadata.EditionRequestEventId ||
                    edition.SeedVersion != metadata.SeedVersion || edition.SeedFingerprint != metadata.SeedFingerprint)
                    throw new InvalidOperationException("Seed metadata does not bind to its edition source.");
                if (edition.Status is "published" or "abandoned" or "cancelled")
                    break;
                edition = edition with
                {
                    Framework = metadata.Framework,
                    CoverageNote = metadata.CoverageNote,
                    SourceUrl = metadata.SourceUrl,
                    ContentRights = metadata.ContentRights,
                    NumberedCriteriaComplete = metadata.NumberedCriteriaComplete
                };
                break;
            case CriteriaSeedDisclosureRecorded disclosure:
                if (source.Stream != CriteriaSeedDisclosure.AddressOf(editionId, disclosure.Category, disclosure.Code) ||
                    ev.Metadata.AggregateId != CriteriaSeedDisclosure.IdentityOf(editionId, disclosure.Category, disclosure.Code) ||
                    edition.DraftRequestEventId != disclosure.EditionRequestEventId ||
                    edition.SeedVersion != disclosure.SeedVersion || edition.SeedFingerprint != disclosure.SeedFingerprint)
                    throw new InvalidOperationException("Seed disclosure does not bind to its edition source.");
                if (edition.Status is "published" or "abandoned" or "cancelled")
                    break;
                var disclosureRow = new RuntimeCriteriaDisclosureIndexRow(_generation, editionId,
                    disclosure.Category, disclosure.Code, disclosure.Note);
                var disclosureKey = new RuntimeCriteriaProjectionSchema.DisclosureKey(_generation, editionId,
                    disclosure.Category, disclosure.Code);
                var oldDisclosure = await RuntimeCriteriaProjectionSchema.Disclosures.GetAsync(Transaction, disclosureKey, ct).ConfigureAwait(false);
                if (oldDisclosure is null)
                    await RuntimeCriteriaProjectionSchema.Disclosures.InsertAsync(Transaction, disclosureRow, ct).ConfigureAwait(false);
                else if (oldDisclosure != disclosureRow)
                    throw new InvalidOperationException("A seed disclosure cannot change its source content.");
                break;
            case CriteriaSeedImportCompleted completed:
                if (source.Stream != CriteriaSeedImport.AddressOf(editionId) ||
                    ev.Metadata.AggregateId != CriteriaSeedImport.IdentityOf(editionId) ||
                    edition.Status != "published" || edition.PublicationEventId != completed.PublicationEventId ||
                    edition.DraftRequestEventId != completed.EditionRequestEventId ||
                    edition.SeedVersion != completed.SeedVersion || edition.SeedFingerprint != completed.SeedFingerprint ||
                    !PlatformCriteriaSeedDefinition.MatchesBinding(editionId, completed.SeedVersion, completed.SeedFingerprint) ||
                    completed.OperationId != PlatformCriteriaSeedDefinition.OperationOf(editionId, "complete") ||
                    completed.RecordedBy != new ActorAttribution("criteria-seed-bootstrap", "system") ||
                    edition.SeedImportCompletionEventId is not null)
                    throw new InvalidOperationException("Seed completion does not match its published edition.");
                edition = edition with { SeedImportCompletionEventId = completed.Metadata.EventId };
                break;
        }
        // A continuation for this edition is invalidated even by a later rejected intent,
        // so no changed row, parent lookup, or disclosure can escape the snapshot check.
        edition = edition with { Revision = checked(previous.Revision + 1) };
        await RuntimeCriteriaProjectionSchema.Editions.ReplaceAsync(Transaction, previous, edition, ct).ConfigureAwait(false);
    }

    async ValueTask<RuntimeCriteriaProjectionClock> AdvanceClockAsync(DomainEventRecord source,
        CancellationToken ct)
    {
        if (source.NextCursor.Value is not { Length: > 0 } sourceCursor)
            throw new InvalidOperationException("A criteria source fact requires a resumable checkpoint.");
        var current = await RuntimeCriteriaProjectionSchema.Clock.GetAsync(Transaction, _generation, ct)
            .ConfigureAwait(false);
        var directoryChange = source.Event is not (CriterionSummaryCorrected or CriterionSummaryUnchanged or
            CriterionSummaryRefused or CriteriaSeedDisclosureRecorded);
        var next = new RuntimeCriteriaProjectionClock(_generation, checked((current?.Ordinal ?? 0) + 1),
            current?.Epoch ?? Uuid.CreateVersion4().ToString(), sourceCursor,
            checked((current?.DirectoryRevision ?? 0) + (directoryChange ? 1 : 0)));
        if (current is null)
            await RuntimeCriteriaProjectionSchema.Clock.InsertAsync(Transaction, next, ct).ConfigureAwait(false);
        else
            await RuntimeCriteriaProjectionSchema.Clock.ReplaceAsync(Transaction, current, next, ct).ConfigureAwait(false);
        return next;
    }

    async ValueTask<RuntimeCriteriaEditionIndexRow> SettleAsync(DomainEventRecord source,
        RuntimeCriteriaEditionIndexRow edition, string identifier, Uuid operation, long generation,
        Uuid proposalEventId, bool accepted, CancellationToken ct)
    {
        RequireEntrySource(source, edition.EditionId, identifier);
        var key = new RuntimeCriteriaProjectionSchema.EntryKey(_generation, edition.EditionId, identifier);
        var row = await RuntimeCriteriaProjectionSchema.Entries.GetAsync(Transaction, key, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A criterion outcome needs its proposal.");
        var beforeCut = row.Pending?.BeforePublication == true;
        var next = RuntimeCriteriaProjectionRules.Settle(row, operation, generation, proposalEventId,
            accepted, edition.Status == "published");
        await RuntimeCriteriaProjectionSchema.Entries.ReplaceAsync(Transaction, row, next, ct).ConfigureAwait(false);
        if (!beforeCut)
            return edition;
        if (edition.PendingBeforeCut <= 0)
            throw new InvalidOperationException("The pending criterion count is inconsistent.");
        return edition with { PendingBeforeCut = edition.PendingBeforeCut - 1 };
    }

    static void RequireEditionSource(DomainEventRecord source, Uuid editionId)
    {
        var owner = new CriteriaEdition(editionId);
        if (source.Stream != owner.Stream || source.Event.Metadata.AggregateId != owner.Id)
            throw new InvalidOperationException("A lifecycle fact must come from its edition owner.");
    }

    static void RequireEntrySource(DomainEventRecord source, Uuid editionId, string identifier)
    {
        var owner = new CriteriaEntry(editionId, identifier);
        if (source.Stream != owner.Stream || source.Event.Metadata.AggregateId != owner.Id)
            throw new InvalidOperationException("A criterion fact must come from its entry owner.");
    }
}
