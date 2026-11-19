using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>System-only fixed-seed binding and exact-source reconciliation.</summary>
/// <remarks>
/// This unregistered adapter conditionally writes the fixed draft request and
/// independent seed metadata/disclosures with qualified heads. It confirms them
/// only after source ordering is qualified. Entry proposals are independently
/// claimed and reconciled; completion is recorded only after each expected
/// seed operation and the exact publication have been verified. The
/// execution context comes from a trusted internal worker, never an API caller.
/// </remarks>
sealed partial class PlatformCriteriaSeedWriter(IExecutionContext context, IAggregateReader aggregates,
    IAggregateExecutor executor, ICriteriaExactSourceReader source, ICriteriaFreshEditionHead? editionHead = null,
    ICriteriaSourceCutCoverage? coverage = null, ICriteriaPublicationAuthority? publications = null,
    ICriteriaParticipantCausalOrder? causalOrder = null, ICriteriaFreshEntryHead? seedFactHead = null,
    ICriteriaFreshLabelClaimHead? seedLabelHead = null) : ICriteriaSeedWriter
{
    static readonly ActorAttribution SeedActor = new("criteria-seed-bootstrap", "system");
    static readonly CriteriaSeedWriteResult Pending = new(CriteriaSeedWriteStatus.Pending, Uuid.Empty);
    static readonly CriteriaSeedWriteResult Conflict = new(CriteriaSeedWriteStatus.Conflict, Uuid.Empty);

    public ValueTask<CriteriaSeedWriteResult> BindEditionAsync(CriteriaSeedHeader header, Uuid operationId, CancellationToken ct) =>
        GuardAsync(() => BindAsync(header, operationId, ct));

    public ValueTask<CriteriaSeedWriteResult> EnsureMetadataAsync(CriteriaSeedHeader header, Uuid bindingEventId,
        Uuid operationId, CancellationToken ct) => GuardAsync(() => MetadataAsync(header, bindingEventId, operationId, ct));

    public ValueTask<CriteriaSeedWriteResult> EnsureDisclosureAsync(CriteriaSeedHeader header, Uuid bindingEventId,
        Uuid operationId, CriteriaSupportGap gap, CancellationToken ct) =>
        GuardAsync(() => DisclosureAsync(header, bindingEventId, operationId, gap, ct));

    public ValueTask<CriteriaSeedWriteResult> EnsureEntryAsync(CriteriaSeedHeader header, Uuid bindingEventId,
        Uuid operationId, Criterion entry, CancellationToken ct) =>
        GuardAsync(() => EntryAsync(header, bindingEventId, operationId, entry, ct));

    public ValueTask<CriteriaSeedWriteResult> PublishAsync(CriteriaSeedHeader header, Uuid bindingEventId,
        Uuid operationId, string witnessIdentifier, CancellationToken ct) =>
        GuardAsync(() => PublicationAsync(header, bindingEventId, operationId, witnessIdentifier, ct));

    public ValueTask<CriteriaSeedWriteResult> ConfirmCoverageAsync(CriteriaSeedHeader header, Uuid bindingEventId,
        CancellationToken ct) => GuardAsync(() => ConfirmSeedCoverageAsync(header, bindingEventId, ct));

    async ValueTask<CriteriaSeedWriteResult> BindAsync(CriteriaSeedHeader header, Uuid operationId, CancellationToken ct)
    {
        if (!PlatformCriteriaSeedDefinition.MatchesHeader(header) ||
            operationId != PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "edition"))
            return Conflict;
        var owner = new CriteriaEdition(header.EditionId, operationId);
        var observation = await ReadBindingAsync(owner, header, ct).ConfigureAwait(false);
        if (observation.Result.Status != CriteriaSeedWriteStatus.Pending)
            return observation.Result;
        // A prior fence or any unknown fact is not proof that the identity is free.
        if (observation.Next != 0 || editionHead is null)
            return Pending;
        var observed = await aggregates.HydrateAsync(owner, ct).ConfigureAwait(false);
        if (observed.CommittedStreamPosition != observation.Next ||
            !await editionHead.IsCurrentAsync(owner.Stream, owner.Id, observation.Next, ct).ConfigureAwait(false))
            return Pending;
        var result = await executor.ExecuteAsync(new CriteriaEdition(header.EditionId, operationId), current =>
        {
            if (current.CommittedStreamPosition != observation.Next)
                return AggregateOutcome.Discard(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The edition source changed before the seed binding.", true)));
            return AggregateOutcome.CommitOnSuccess(current.DraftSeed(operationId, header.EditionLabel,
                header.SeedVersion, header.SeedFingerprint));
        }, context, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return Pending;
        // Dispatch acknowledgement is not a source fact.
        return (await ReadBindingAsync(owner, header, ct).ConfigureAwait(false)).Result;
    }

    async ValueTask<(ulong Next, CriteriaSeedWriteResult Result)> ReadBindingAsync(CriteriaEdition owner,
        CriteriaSeedHeader header, CancellationToken ct)
    {
        ulong next = 0;
        var result = Pending;
        await foreach (var record in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            Check(record, owner.Stream, owner.Id, next++);
            if (record.Event is not CriteriaEditionDraftRequested request)
                continue;
            if (result.Status != CriteriaSeedWriteStatus.Pending)
                throw new InvalidOperationException("An edition has multiple draft requests.");
            result = await VerifyBindingAsync(header, request.Metadata.EventId, ct).ConfigureAwait(false);
        }
        return (next, result);
    }

    async ValueTask<CriteriaSeedWriteResult> MetadataAsync(CriteriaSeedHeader header, Uuid bindingId,
        Uuid operationId, CancellationToken ct)
    {
        if (!PlatformCriteriaSeedDefinition.MatchesHeader(header) ||
            operationId != PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "metadata"))
            return Conflict;
        var binding = await VerifyBindingAsync(header, bindingId, ct).ConfigureAwait(false);
        if (binding.Status != CriteriaSeedWriteStatus.Confirmed)
            return binding;
        var owner = new CriteriaSeedMetadata(header.EditionId);
        var observed = await ReadMetadataAsync(owner, header, bindingId, operationId, ct).ConfigureAwait(false);
        if (observed.Result.Status != CriteriaSeedWriteStatus.Pending || observed.Next != 0)
            return observed.Result;
        if (!await CanWriteFactAsync(owner, observed.Next, header, bindingId, ct).ConfigureAwait(false))
            return Pending;
        var result = await executor.ExecuteAsync(new CriteriaSeedMetadata(header.EditionId), current =>
        {
            if (current.CommittedStreamPosition != observed.Next)
                return AggregateOutcome.Discard(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The seed metadata source changed before append.", true)));
            return AggregateOutcome.CommitOnSuccess(current.Record(operationId, header.SeedVersion, header.SeedFingerprint,
                bindingId, header.Framework, header.PublishedAt, header.CoverageNote, header.SourceUrl, header.ContentRights,
                header.NumberedCriteriaComplete, SeedActor));
        }, context, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return Pending;
        return (await ReadMetadataAsync(owner, header, bindingId, operationId, ct).ConfigureAwait(false)).Result;
    }

    async ValueTask<(ulong Next, CriteriaSeedWriteResult Result)> ReadMetadataAsync(CriteriaSeedMetadata owner,
        CriteriaSeedHeader header, Uuid bindingId, Uuid operationId, CancellationToken ct)
    {
        ulong next = 0;
        await foreach (var record in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            Check(record, owner.Stream, owner.Id, next++);
            if (record.Event is not CriteriaSeedMetadataRecorded fact)
                return (next, Conflict);
            if (fact.EditionId != header.EditionId || fact.OperationId != operationId || fact.SeedVersion != header.SeedVersion ||
                fact.SeedFingerprint != header.SeedFingerprint || fact.EditionRequestEventId != bindingId ||
                fact.Framework != header.Framework || fact.PublishedAt != header.PublishedAt || fact.CoverageNote != header.CoverageNote ||
                fact.SourceUrl != header.SourceUrl || fact.ContentRights != header.ContentRights ||
                fact.NumberedCriteriaComplete != header.NumberedCriteriaComplete || fact.RecordedBy != SeedActor)
                return (next, Conflict);
            return (next, await PreTerminalAsync(header, bindingId, record.AreaOffset, ct).ConfigureAwait(false)
                ? Confirmed(fact.Metadata.EventId) : Pending);
        }
        return (next, Pending);
    }

    async ValueTask<CriteriaSeedWriteResult> DisclosureAsync(CriteriaSeedHeader header, Uuid bindingId,
        Uuid operationId, CriteriaSupportGap gap, CancellationToken ct)
    {
        if (!PlatformCriteriaSeedDefinition.MatchesHeader(header) ||
            !PlatformCriteriaSeedDefinition.MatchesDisclosure(header.EditionId, header.SeedVersion, header.SeedFingerprint, gap) ||
            operationId != PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "disclosure:" + gap.Category + ":" + gap.Code))
            return Conflict;
        var binding = await VerifyBindingAsync(header, bindingId, ct).ConfigureAwait(false);
        if (binding.Status != CriteriaSeedWriteStatus.Confirmed)
            return binding;
        var owner = new CriteriaSeedDisclosure(header.EditionId, gap.Category, gap.Code);
        var observed = await ReadDisclosureAsync(owner, header, bindingId, operationId, gap, ct).ConfigureAwait(false);
        if (observed.Result.Status != CriteriaSeedWriteStatus.Pending || observed.Next != 0)
            return observed.Result;
        if (!await CanWriteFactAsync(owner, observed.Next, header, bindingId, ct).ConfigureAwait(false))
            return Pending;
        var result = await executor.ExecuteAsync(new CriteriaSeedDisclosure(header.EditionId, gap.Category, gap.Code), current =>
        {
            if (current.CommittedStreamPosition != observed.Next)
                return AggregateOutcome.Discard(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The seed disclosure source changed before append.", true)));
            return AggregateOutcome.CommitOnSuccess(current.Record(operationId, header.SeedVersion, header.SeedFingerprint,
                bindingId, gap.Note, SeedActor));
        }, context, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return Pending;
        return (await ReadDisclosureAsync(owner, header, bindingId, operationId, gap, ct).ConfigureAwait(false)).Result;
    }

    async ValueTask<(ulong Next, CriteriaSeedWriteResult Result)> ReadDisclosureAsync(CriteriaSeedDisclosure owner,
        CriteriaSeedHeader header, Uuid bindingId, Uuid operationId, CriteriaSupportGap gap, CancellationToken ct)
    {
        ulong next = 0;
        await foreach (var record in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            Check(record, owner.Stream, owner.Id, next++);
            if (record.Event is not CriteriaSeedDisclosureRecorded fact)
                return (next, Conflict);
            if (fact.EditionId != header.EditionId || fact.Category != gap.Category || fact.Code != gap.Code ||
                fact.OperationId != operationId || fact.SeedVersion != header.SeedVersion || fact.SeedFingerprint != header.SeedFingerprint ||
                fact.EditionRequestEventId != bindingId || fact.Note != gap.Note || fact.RecordedBy != SeedActor)
                return (next, Conflict);
            return (next, await PreTerminalAsync(header, bindingId, record.AreaOffset, ct).ConfigureAwait(false)
                ? Confirmed(fact.Metadata.EventId) : Pending);
        }
        return (next, Pending);
    }

    async ValueTask<bool> CanWriteFactAsync<TAggregate>(TAggregate target, ulong targetNext,
        CriteriaSeedHeader header, Uuid bindingId, CancellationToken ct) where TAggregate : Aggregate
    {
        if (coverage is null || editionHead is null || seedFactHead is null || targetNext != 0)
            return false;
        var edition = new CriteriaEdition(header.EditionId);
        ulong editionNext = 0;
        ulong? headArea = null;
        var bindingFound = false;
        await foreach (var record in source.ReadAsync(edition.Stream, edition.Id, ct).ConfigureAwait(false))
        {
            Check(record, edition.Stream, edition.Id, editionNext++);
            headArea = record.AreaOffset;
            if (record.Event.Metadata.EventId == bindingId && record.Event is CriteriaEditionDraftRequested)
                bindingFound = true;
            if (record.Event is CriteriaEditionPublished or CriteriaEditionDraftAbandoned or CriteriaEditionDraftRequestCancelled)
                return false;
        }
        if (!bindingFound || headArea is null ||
            !await editionHead.IsCurrentAsync(edition.Stream, edition.Id, editionNext, ct).ConfigureAwait(false) ||
            !await coverage.CoversAsync(edition.Stream, editionNext, headArea.Value, ct).ConfigureAwait(false))
            return false;
        var observed = await aggregates.HydrateAsync(target, ct).ConfigureAwait(false);
        return observed.CommittedStreamPosition == targetNext &&
            await seedFactHead.IsCurrentAsync(target.Stream, target.Id, targetNext, ct).ConfigureAwait(false);
    }

    async ValueTask<bool> PreTerminalAsync(CriteriaSeedHeader header, Uuid bindingId, ulong factArea, CancellationToken ct)
    {
        if (coverage is null)
            return false;
        var edition = new CriteriaEdition(header.EditionId);
        ulong next = 0;
        var sawBinding = false;
        await foreach (var record in source.ReadAsync(edition.Stream, edition.Id, ct).ConfigureAwait(false))
        {
            Check(record, edition.Stream, edition.Id, next++);
            if (record.Event.Metadata.EventId == bindingId && record.AreaOffset < factArea)
                sawBinding = true;
            if (record.AreaOffset < factArea && record.Event is (CriteriaEditionPublished or CriteriaEditionDraftAbandoned or
                CriteriaEditionDraftRequestCancelled))
                return false;
        }
        return sawBinding && await coverage.CoversAsync(edition.Stream, next, factArea, ct).ConfigureAwait(false);
    }

    async ValueTask<CriteriaSeedWriteResult> EntryAsync(CriteriaSeedHeader header, Uuid bindingId,
        Uuid operationId, Criterion entry, CancellationToken ct)
    {
        if (!PlatformCriteriaSeedDefinition.MatchesHeader(header) ||
            !PlatformCriteriaSeedDefinition.MatchesEntry(header.EditionId, header.SeedVersion, header.SeedFingerprint, entry) ||
            operationId != PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "entry:" + entry.Identifier))
            return Conflict;
        var binding = await VerifyBindingAsync(header, bindingId, ct).ConfigureAwait(false);
        if (binding.Status != CriteriaSeedWriteStatus.Confirmed)
            return binding;
        if (coverage is null || publications is null || causalOrder is null)
            return Pending;
        var owner = new CriteriaEntry(header.EditionId, entry.Identifier, operationId);
        var history = await ReadEntryAsync(owner, operationId, ct).ConfigureAwait(false);
        if (history.Foreign)
            return Pending;
        if (history.Proposal is null)
            return await ProposeSeedEntryAsync(header, bindingId, entry, owner, ct).ConfigureAwait(false);
        var proposal = history.Proposal;
        var outcome = history.Outcome;
        if (proposal.Event is not CriterionSeedProposed seed || outcome is null)
            return Pending;
        if (seed.EditionRequestEventId != bindingId || !PlatformCriteriaSeedDefinition.MatchesProposal(seed))
            return Pending;
        if (!await new CriteriaSeedBindingVerifier(source).VerifyProposalAsync(seed, proposal.AreaOffset, ct).ConfigureAwait(false))
            return Pending;
        if (seed.Intent.Kind == "point_of_focus" && !await CriteriaEntryDecisionAuthority.ParentActiveAsync(source,
                coverage, publications, seed.Intent, seed.ParentAcceptedEventId, proposal.AreaOffset, ct, causalOrder).ConfigureAwait(false))
            return Pending;
        var verified = await new CriteriaEntryDecisionEvidenceVerifier(source, coverage, publications, causalOrder)
            .VerifyAsync(proposal, outcome, ct).ConfigureAwait(false);
        if (verified != CriteriaEntryDecisionVerification.Verified || outcome.Event is not CriterionChanged changed)
            return Pending;
        return Confirmed(changed.Metadata.EventId);
    }

    async ValueTask<(ulong Next, CriteriaOrderedSourceEvent? Proposal, CriteriaOrderedSourceEvent? Outcome, bool Foreign)> ReadEntryAsync(
        CriteriaEntry owner, Uuid operationId, CancellationToken ct)
    {
        CriteriaOrderedSourceEvent? proposal = null, outcome = null;
        var foreign = false;
        ulong next = 0;
        await foreach (var record in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            Check(record, owner.Stream, owner.Id, next++);
            if (record.Event is CriterionSeedProposed candidate && candidate.Intent.OperationId == operationId)
            {
                if (proposal is not null)
                    throw new InvalidOperationException("A seed entry has duplicate proposals.");
                proposal = record;
            }
            if (record.Event is CriterionChangeProposed human && human.Intent.OperationId == operationId)
                foreign = true;
            var proposalId = record.Event switch
            {
                CriterionChanged accepted => accepted.ProposalEventId,
                CriterionChangeRefused refused => refused.ProposalEventId,
                _ => Uuid.Empty,
            };
            if (proposal is not null && proposalId == proposal.Event.Metadata.EventId)
            {
                if (outcome is not null)
                    throw new InvalidOperationException("A seed proposal has duplicate outcomes.");
                outcome = record;
            }
        }
        return (next, proposal, outcome, foreign);
    }

    async ValueTask<CriteriaSeedWriteResult> PublicationAsync(CriteriaSeedHeader header, Uuid bindingId,
        Uuid operationId, string witnessIdentifier, CancellationToken ct)
    {
        if (!PlatformCriteriaSeedDefinition.MatchesHeader(header) ||
            operationId != PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "publication"))
            return Conflict;
        var binding = await VerifyBindingAsync(header, bindingId, ct).ConfigureAwait(false);
        if (binding.Status != CriteriaSeedWriteStatus.Confirmed)
            return binding;
        if (publications is null)
            return Pending;
        var proof = await publications.ReadPublishedAsync(header.EditionId, ct).ConfigureAwait(false);
        if (!proof.IsSuccess || proof.Value is null)
            return await StartSeedPublicationAsync(header, bindingId, operationId, witnessIdentifier, ct).ConfigureAwait(false);
        var owner = new CriteriaEdition(header.EditionId);
        var record = await source.FindAsync(owner.Stream, owner.Id, proof.Value.EventId, ct).ConfigureAwait(false);
        if (record?.Event is not CriteriaEditionPublished fact || record.Stream != owner.Stream ||
            record.ResourceOffset != proof.Value.ResourceOffset || fact.Metadata.EventId != proof.Value.EventId ||
            fact.Metadata.AggregateId != owner.Id || fact.Metadata.IsAudit ||
            fact.EditionId != header.EditionId || fact.DraftGeneration != PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "edition") ||
            fact.PublicationAttemptId != operationId || fact.WitnessIdentifier != witnessIdentifier || fact.PublishedBy != SeedActor)
            return Pending;
        return Confirmed(fact.Metadata.EventId);
    }

    ValueTask<CriteriaSeedWriteResult> VerifyBindingAsync(CriteriaSeedHeader header, Uuid bindingId, CancellationToken ct) =>
        new CriteriaSeedBindingVerifier(source).VerifyAsync(header, bindingId, ct);

    async ValueTask<CriteriaSeedWriteResult> GuardAsync(Func<ValueTask<CriteriaSeedWriteResult>> action)
    {
        if (!RequestActor.IsSystem(context.Actor))
            throw new UnauthorizedAccessException("A trusted system worker is required.");
        try
        { return await action().ConfigureAwait(false); }
        catch (CriteriaSourceUnavailableException) { return Pending; }
        catch (EventStreamConcurrencyException) { return Pending; }
        catch (FitzException) { return Pending; }
        catch (IOException) { return Pending; }
        catch (TimeoutException) { return Pending; }
    }

    static CriteriaSeedWriteResult Confirmed(Uuid id) => id == Uuid.Empty ? Pending : new(CriteriaSeedWriteStatus.Confirmed, id);
    static void Check(CriteriaOrderedSourceEvent record, EventStreamAddress stream, Uuid id, ulong position)
    {
        if (record.Stream != stream || record.ResourceOffset != position || record.Event.Metadata.AggregateId != id ||
            record.Event.Metadata.EventId == Uuid.Empty || record.Event.Metadata.IsAudit ||
            record.Event.Metadata.AggregateVersion != checked(position + 1))
            throw new InvalidOperationException("The seed source is not an exact contiguous owner history.");
    }
}
