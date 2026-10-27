using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Readers;

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
        Uuid acceptedCommandId = default, string? commandKind = null,
        string? authoredIntentSha256 = null, CancellationToken ct = default)
    {
        var lines = new HashSet<Uuid>();
        var admitted = new Dictionary<Uuid, OperationProvenance>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           AccessExpectation.Area), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            var scope = record.Event switch
            {
                AccessExpectationProposed proposal => (proposal.TenantId, proposal.SystemInstanceId, proposal.Revision,
                    nameof(ProposeAccessExpectation)),
                AccessExpectationApproved approval => (approval.TenantId, approval.SystemInstanceId, approval.Revision,
                    nameof(ApproveAccessExpectation)),
                AccessExpectationExceptionRecorded exception => (exception.TenantId, exception.SystemInstanceId,
                    exception.Revision, nameof(ExemptAccessExpectation)),
                _ => (Uuid.Empty, Uuid.Empty, 0L, string.Empty),
            };
            if (scope.Item2 != systemInstanceId)
                continue;
            if (scope.Item1 != tenantId || scope.Item3 < 1 ||
                !Uuid.TryParse(record.Stream.Resource, CultureInfo.InvariantCulture, out var line) || line == Uuid.Empty ||
                record.Stream.Realm != tenantId.ToString() || record.Stream.Area != AccessExpectation.Area ||
                record.Event.Metadata.AggregateId != line || record.Event.Metadata.EventId == Uuid.Empty ||
                record.Event.Metadata.AggregateVersion != (ulong)scope.Item3 ||
                record.Event is not INativeOperationFact native || !NativeOwnerHistory.IsOwnRoot(
                    new AccessExpectation(tenantId, line), native.Operation, scope.Item4, (ulong)scope.Item3 - 1))
                throw new ImmutableContentIntegrityException("The expectation locator has another exact native scope or source identity.");
            if (record.Event is AccessExpectationProposed proposed && proposed.Operation.LogicalId == expectationId)
                lines.Add(line);
            if (acceptedCommandId == Uuid.Empty || native.Operation.LogicalId != acceptedCommandId ||
                native.Operation.CommandKind != commandKind)
                continue;
            if (!admitted.TryAdd(line, native.Operation))
                throw new ImmutableContentIntegrityException("The expectation command is accepted more than once on one line.");
        }
        if (admitted.Count == 1)
            return admitted.Keys.Single();
        if (admitted.Count > 1)
        {
            var exact = admitted.Where(pair => pair.Value.AuthoredIntentSha256 == authoredIntentSha256)
                .Select(static pair => pair.Key).ToArray();
            if (exact.Length == 1)
                return exact[0];
            throw new ImmutableContentIntegrityException("The scoped accepted expectation command has ambiguous first owners.");
        }
        if (lines.Count > 1)
            throw new ImmutableContentIntegrityException("The scoped expectation identity belongs to multiple authoritative lines.");
        return lines.Count == 0 ? null : lines.Single();
    }

    /// <summary>The hydrated line selected for one logical operation and its folded state.</summary>
    public async ValueTask<(AccessExpectation Owner, AccessExpectationLine Line)> ReadLineAsync(Uuid tenantId,
        Uuid lineId, Uuid selectedLogicalId, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new AccessExpectation(tenantId, lineId, selectedLogicalId), ct)
            .ConfigureAwait(false);
        return (owner, await AccessExpectationLine.ReadAsync(owner, events, content, tenantId, ct).ConfigureAwait(false));
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
