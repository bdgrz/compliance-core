using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Resolves expectation lines and composes a system instance's expectations and missing-population exceptions
///     from their own owner streams. Streams are discovered from the committed proposal and exception facts of the
///     tenant's areas; every view is verified immutable content.
/// </summary>
public sealed class AccessExpectationsReader(IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content)
{
    /// <summary>
    ///     The line that holds an expectation of the system instance, or null when no such proposal exists. The
    ///     expectation id is the logical id of its proposal fact, so the resolution never depends on a projection.
    /// </summary>
    public async ValueTask<Uuid?> ResolveLineAsync(Uuid tenantId, Uuid systemInstanceId, Uuid expectationId,
        CancellationToken ct = default)
    {
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           AccessExpectation.Area), EventCursor.Start, ct).ConfigureAwait(false))
            if (!record.Event.Metadata.IsAudit && record.Event is AccessExpectationProposed proposed &&
                proposed.SystemInstanceId == systemInstanceId && proposed.Operation.LogicalId == expectationId)
                return Uuid.Parse(record.Stream.Resource, System.Globalization.CultureInfo.InvariantCulture);
        return null;
    }

    /// <summary>The hydrated line selected for one logical operation and its folded state.</summary>
    public async ValueTask<(AccessExpectation Owner, AccessExpectationLine Line)> ReadLineAsync(Uuid tenantId,
        Uuid lineId, Uuid selectedLogicalId, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new AccessExpectation(tenantId, lineId, selectedLogicalId), ct)
            .ConfigureAwait(false);
        return (owner, await AccessExpectationLine.ReadAsync(owner, content, tenantId, ct).ConfigureAwait(false));
    }

    /// <summary>Composes every expectation line, expectation exception and population exception of the instance.</summary>
    public async ValueTask<AccessExpectationsSnapshot> ReadAsync(Uuid tenantId, Uuid systemInstanceId,
        CancellationToken ct = default)
    {
        var lines = new SortedSet<Uuid>(Comparer<Uuid>.Create(static (left, right) =>
            string.CompareOrdinal(left.ToString(), right.ToString())));
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           AccessExpectation.Area), EventCursor.Start, ct).ConfigureAwait(false))
            if (!record.Event.Metadata.IsAudit && record.Event is AccessExpectationProposed proposed &&
                proposed.SystemInstanceId == systemInstanceId)
                lines.Add(Uuid.Parse(record.Stream.Resource, System.Globalization.CultureInfo.InvariantCulture));

        var expectations = new List<AccessExpectationView>();
        var exceptions = new List<AccessExpectationExceptionView>();
        foreach (var lineId in lines)
        {
            var (_, line) = await ReadLineAsync(tenantId, lineId, Uuid.Empty, ct).ConfigureAwait(false);
            expectations.AddRange(line.Expectations);
            exceptions.AddRange(line.Exceptions);
        }

        var populationExceptions = new List<AccessPopulationExceptionView>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           AccessPopulationException.Area), EventCursor.Start, ct).ConfigureAwait(false))
            if (!record.Event.Metadata.IsAudit && record.Event is AccessPopulationExceptionRecorded recorded &&
                recorded.SystemInstanceId == systemInstanceId)
                populationExceptions.Add(await AccessReviewContent.ReadAsync<AccessPopulationExceptionView>(content,
                    tenantId, recorded.ExceptionContent, ct).ConfigureAwait(false));

        return new AccessExpectationsSnapshot(tenantId, systemInstanceId,
            [.. expectations.OrderBy(static item => item.ProposedAt).ThenBy(static item => item.ExpectationId.ToString(),
                StringComparer.Ordinal)],
            [.. exceptions.OrderBy(static item => item.ApprovedAt).ThenBy(static item => item.ExceptionId.ToString(),
                StringComparer.Ordinal)],
            [.. populationExceptions.OrderBy(static item => item.ApprovedAt).ThenBy(
                static item => item.ExceptionId.ToString(), StringComparer.Ordinal)]);
    }
}
