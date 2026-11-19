using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

sealed partial class PlatformCriteriaSeedWriter
{
    async ValueTask<CriteriaSeedWriteResult> ConfirmSeedCoverageAsync(CriteriaSeedHeader header, Uuid bindingId,
        CancellationToken ct)
    {
        var binding = await VerifyBindingAsync(header, bindingId, ct).ConfigureAwait(false);
        if (binding.Status != CriteriaSeedWriteStatus.Confirmed)
            return binding;
        if (seedFactHead is null || publications is null || coverage is null || causalOrder is null)
            return Pending;
        var witness = PlatformCriteriaCatalog.EnumerateSeedEntries().FirstOrDefault(entry => entry.Kind == "criterion");
        if (witness is null)
            return Pending;
        var publication = await PublicationAsync(header, bindingId,
            PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "publication"), witness.Identifier, ct).ConfigureAwait(false);
        if (publication.Status != CriteriaSeedWriteStatus.Confirmed)
            return Pending;
        var edition = new CriteriaEdition(header.EditionId);
        var published = await source.FindAsync(edition.Stream, edition.Id, publication.EvidenceId, ct).ConfigureAwait(false);
        if (published?.Event is not CriteriaEditionPublished)
            return Pending;

        // Verify each independently owned fact and outcome, one at a time. The
        // resulting scalar fact is not a roster and says nothing about other entries.
        var metadata = await MetadataAsync(header, bindingId,
            PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "metadata"), ct).ConfigureAwait(false);
        if (metadata.Status != CriteriaSeedWriteStatus.Confirmed)
            return Pending;
        foreach (var gap in PlatformCriteriaCatalog.SeedEdition().SupportGaps)
        {
            var operation = PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "disclosure:" + gap.Category + ":" + gap.Code);
            if ((await DisclosureAsync(header, bindingId, operation, gap, ct).ConfigureAwait(false)).Status != CriteriaSeedWriteStatus.Confirmed)
                return Pending;
        }
        foreach (var entry in PlatformCriteriaCatalog.EnumerateSeedEntries())
        {
            var operation = PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "entry:" + entry.Identifier);
            if ((await EntryAsync(header, bindingId, operation, entry, ct).ConfigureAwait(false)).Status != CriteriaSeedWriteStatus.Confirmed)
                return Pending;
        }

        var owner = new CriteriaSeedImport(header.EditionId);
        var operationId = PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "complete");
        ulong next = 0;
        CriteriaSeedImportCompleted? prior = null;
        await foreach (var record in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            Check(record, owner.Stream, owner.Id, next++);
            if (prior is not null || record.Event is not CriteriaSeedImportCompleted completed ||
                record.AreaOffset <= published.AreaOffset)
                return Pending;
            prior = completed;
        }
        if (prior is not null)
            return prior.EditionId == header.EditionId && prior.OperationId == operationId &&
                prior.EditionRequestEventId == bindingId && prior.SeedVersion == header.SeedVersion &&
                prior.SeedFingerprint == header.SeedFingerprint && prior.PublicationEventId == publication.EvidenceId &&
                prior.RecordedBy == SeedActor ? Confirmed(prior.Metadata.EventId) : Conflict;
        if (next != 0)
            return Pending;
        var loaded = await aggregates.HydrateAsync(owner, ct).ConfigureAwait(false);
        if (loaded.CommittedStreamPosition != next ||
            !await seedFactHead.IsCurrentAsync(owner.Stream, owner.Id, next, ct).ConfigureAwait(false))
            return Pending;
        var result = await executor.ExecuteAsync(new CriteriaSeedImport(header.EditionId), current =>
        {
            if (current.CommittedStreamPosition != next)
                return AggregateOutcome.Discard(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The seed completion changed before append.", true)));
            return AggregateOutcome.CommitOnSuccess(current.Complete(operationId, bindingId, header.SeedVersion,
                header.SeedFingerprint, publication.EvidenceId));
        }, context, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return Pending;
        // A successful dispatch is not an observed source fact; retry reads its exact owner.
        return Pending;
    }
}
