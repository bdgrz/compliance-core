using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Reads the exact committed fact named by a label-coordination message.</summary>
/// <remarks>
/// The caller must compute the expected source stream from the edition or label identity and
/// verify the returned payload's exact operation and generation before issuing a participant
/// command. An absent record is only an unobserved outcome; it is not a refusal or a fence.
/// </remarks>
sealed class CriteriaLabelSourceVerifier(IDomainEventReader reader)
{
    /// <summary>Allows an exact current-format start or a source-verified fixed seed start.</summary>
    internal async ValueTask<bool> AllowsPublicationAsync(CriteriaPublicationStarted started, CancellationToken ct)
    {
        if (started.Admission is not null)
            return CriteriaParticipantAdmission.AllowsHumanPublication(started);
        var seed = PlatformCriteriaCatalog.SeedEdition();
        if (started.EditionId != seed.EditionId || started.DraftGeneration != PlatformCriteriaSeedDefinition.OperationOf(seed.EditionId, "edition") ||
            started.PublicationAttemptId != PlatformCriteriaSeedDefinition.OperationOf(seed.EditionId, "publication") ||
            started.Actor != new ActorAttribution("criteria-seed-bootstrap", "system"))
            return false;
        var stream = CriteriaEdition.AddressOf(seed.EditionId);
        var found = false;
        ulong position = 0;
        await foreach (var record in reader.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (record.Stream != stream || record.ResourceOffset != position++)
                return false;
            if (record.Event is CriteriaEditionDraftRequested draft && !draft.Metadata.IsAudit &&
                draft.Metadata.AggregateId == seed.EditionId && CriteriaParticipantAdmission.IsSeedDraft(draft))
                found = true;
            if (record.Event.Metadata.EventId == started.Metadata.EventId)
                return found && !record.Event.Metadata.IsAudit && record.Event is CriteriaPublicationStarted actual && actual == started;
        }
        return false;
    }

    /// <summary>Finds the exact committed start preceding an abort before allowing its cleanup.</summary>
    internal async ValueTask<bool> AllowsAbortAsync(CriteriaPublicationAborted aborted, CancellationToken ct)
    {
        var stream = CriteriaEdition.AddressOf(aborted.EditionId);
        CriteriaPublicationStarted? start = null;
        ulong position = 0;
        await foreach (var record in reader.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (record.Stream != stream || record.ResourceOffset != position++)
                return false;
            if (record.Event is CriteriaPublicationStarted candidate && candidate.EditionId == aborted.EditionId &&
                candidate.DraftGeneration == aborted.DraftGeneration && candidate.PublicationAttemptId == aborted.PublicationAttemptId &&
                candidate.AttemptOrdinal == aborted.AttemptOrdinal && candidate.WitnessIdentifier == aborted.WitnessIdentifier &&
                candidate.WitnessIncarnationId == aborted.WitnessIncarnationId && !candidate.Metadata.IsAudit &&
                candidate.Metadata.AggregateId == aborted.EditionId)
                start = candidate;
            if (record.Event.Metadata.EventId == aborted.Metadata.EventId)
                return start is not null && !record.Event.Metadata.IsAudit && record.Event is CriteriaPublicationAborted actual &&
                    actual == aborted && await AllowsPublicationAsync(start, ct).ConfigureAwait(false);
        }
        return false;
    }
    /// <summary>
    /// Finds one identified event by streaming a low-churn participant's own history.
    /// A missing result is unobserved, not evidence that a delayed event cannot arrive.
    /// </summary>
    public async ValueTask<TEvent?> FindAsync<TEvent>(EventStreamAddress expectedStream, Uuid eventId,
        CancellationToken ct)
        where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(expectedStream);
        if (eventId == Uuid.Empty)
            throw new ArgumentException("An exact source event identity is required.", nameof(eventId));

        ulong expectedPosition = 0;
        await foreach (var record in reader.ReadAsync(expectedStream, 0, ct).ConfigureAwait(false))
        {
            if (record.Stream != expectedStream || record.ResourceOffset != expectedPosition)
                throw new InvalidOperationException("The label coordination source is not a contiguous exact stream.");
            expectedPosition = checked(expectedPosition + 1);

            if (record.Event.Metadata.EventId != eventId)
                continue;
            if (record.Event.Metadata.IsAudit || record.Event is not TEvent typed)
                throw new InvalidOperationException("The label coordination source does not match the required fact type.");
            return typed;
        }

        return null;
    }

    /// <summary>Returns the committed typed event, or null when that physical record is not observed.</summary>
    public async ValueTask<TEvent?> ReadAsync<TEvent>(EventStreamAddress expectedStream, ulong physicalPosition,
        Uuid eventId, CancellationToken ct)
        where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(expectedStream);
        if (eventId == Uuid.Empty)
            throw new ArgumentException("An exact source event identity is required.", nameof(eventId));

        await using var records = reader.ReadAsync(expectedStream, physicalPosition, ct).GetAsyncEnumerator(ct);
        if (!await records.MoveNextAsync().ConfigureAwait(false))
            return null;

        var record = records.Current;
        if (record.Stream != expectedStream || record.ResourceOffset != physicalPosition ||
            record.Event.Metadata.EventId != eventId || record.Event.Metadata.IsAudit ||
            record.Event is not TEvent typed)
            throw new InvalidOperationException("The label coordination source does not match the exact committed fact.");

        return typed;
    }
}
