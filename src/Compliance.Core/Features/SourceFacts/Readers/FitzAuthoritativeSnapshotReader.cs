using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
using Cntryl.Fitz;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.SourceFacts.Readers;

/// <summary>
/// Fixed-cut reads over <see cref="IStreamClient"/>. Fitz continuation tokens apply only to global selectors, so
/// every scan is global and the requested realm is verified per record before anything is deserialized.
/// </summary>
sealed class FitzAuthoritativeSnapshotReader(IStreamClient streams, IDomainEventSerializer serializer,
    TimeProvider? time = null) : IAuthoritativeSnapshotReader
{
    public const string AnchorRoute = "stream://**";
    const ulong PageSize = 256;

    public async Task<SnapshotCapture> CaptureAsync(CancellationToken ct = default)
    {
        // Fitz 4.3 exposes replay-to-caught-up rather than a detached cursor carrying a watermark.
        // Consume one immutable global replay and use its terminal position as the exclusive cut.
        ulong watermark = 0;
        ulong next = 0;
        await foreach (var batch in streams.ReadAsync(AnchorRoute, next, StreamReadMode.Replay,
                           new StreamReadOptions { BatchSize = PageSize }, ct).ConfigureAwait(false))
        {
            if (batch.FromOffset != next || batch.NextOffset < next)
                throw new SourceFactVerificationException("The broker returned a non-contiguous global replay cursor.");
            next = batch.NextOffset;
            watermark = next;
        }
        return new SnapshotCapture(
            new AuthoritativeReadCut(AnchorRoute, watermark, (time ?? TimeProvider.System).GetUtcNow()), null);
    }

    public async Task<SnapshotReadResult> ReadAtAsync(SnapshotSelector selector, string tenantScope,
        AuthoritativeReadCut cut, SnapshotCursor? cursor = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantScope);
        ArgumentNullException.ThrowIfNull(cut);
        var route = selector.Route;
        var watermark = cut.GlobalWatermarkExclusive;
        if (cursor is not null && (!string.Equals(cursor.Route, route, StringComparison.Ordinal) ||
            !string.Equals(cursor.TenantScope, tenantScope, StringComparison.Ordinal) ||
            cursor.CutWatermarkExclusive != watermark))
            throw new SourceFactVerificationException("The cursor belongs to a different selector, tenant or cut.");
        var state = cursor ?? new SnapshotCursor(route, 0, null, null, tenantScope, watermark);
        var facts = new List<ResolvedSourceFact>();
        var nextResource = new Dictionary<string, ulong>(StringComparer.Ordinal);
        var fresh = cursor is null;
        var next = state.NextGlobalOffset;
        if (next >= watermark)
            return new SnapshotReadResult(facts, state with
            {
                Fingerprint = null,
                CapturedWatermark = null,
            }, true);
        var reachedCut = false;
        await foreach (var batch in streams.ReadAsync(route, next, StreamReadMode.Replay,
                           new StreamReadOptions { BatchSize = 1 }, ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            var from = next;
            if (batch.FromOffset != from || batch.NextOffset < from)
                throw new SourceFactVerificationException("The broker returned a non-contiguous selector replay cursor.");
            if (batch.Items.Count > 1)
                throw new SourceFactVerificationException("The broker returned more items than the bounded snapshot read requested.");
            foreach (var item in batch.Items)
            {
                var address = Address(item.Route);
                var ownScope = string.Equals(address.Realm, tenantScope, StringComparison.Ordinal);
                if (item.Kind != StreamReadItemKind.Event || item.Record is not { } record)
                {
                    if (!ownScope)
                        continue;
                    if (item.Kind == StreamReadItemKind.FilteredRange && batch.NextOffset > watermark)
                        throw new SourceFactVerificationException(
                            "A withheld range overlaps the fixed cut, so source coverage cannot be verified.");
                    if (batch.NextOffset > watermark)
                    {
                        reachedCut = true;
                        break;
                    }
                    throw new SourceFactVerificationException(
                        "A relevant record was withheld, so the source coverage is incomplete.");
                }
                if (record.GlobalOffset is not { } globalOffset || record.RealmOffset is not { } realmOffset)
                    throw new SourceFactVerificationException("The broker did not report realm and global offsets.");
                if (globalOffset < next)
                    throw new SourceFactVerificationException("The broker returned records out of global order.");
                if (globalOffset >= watermark)
                {
                    reachedCut = true;
                    break;
                }
                next = globalOffset + 1;
                if (!ownScope)
                    continue;
                CheckContinuity(nextResource, record, fresh);
                var domainEvent = Deserialize(record.Body);
                if (domainEvent.Metadata is { IsAudit: true })
                    continue;
                facts.Add(SourceFactVerifier.Build(address, record.Offset, realmOffset, globalOffset, domainEvent));
            }
            if (reachedCut || batch.CaughtUp || batch.NextOffset >= watermark)
                break;
            if (batch.NextOffset <= from)
                throw new SourceFactVerificationException("The broker page made no progress.");
            next = batch.NextOffset;
        }
        // Replay reaches the broker's current tail. Since it began after capture and records are append-only,
        // it covers the fixed cut; an empty scan checkpoints the watermark itself (zero), never offset 1.
        return new SnapshotReadResult(facts, state with
        {
            NextGlobalOffset = watermark,
            Fingerprint = null,
            CapturedWatermark = null,
        }, true);
    }

    static void CheckContinuity(Dictionary<string, ulong> nextResource, StreamRecord record, bool fresh)
    {
        if (nextResource.TryGetValue(record.Route, out var expected))
        {
            if (record.Offset != expected)
                throw new SourceFactVerificationException(
                    $"A record is missing from {record.Route} before offset {record.Offset}.");
        }
        else if (fresh && record.Offset != 0)
        {
            throw new SourceFactVerificationException(
                $"The oldest records of {record.Route} are no longer retained.");
        }
        nextResource[record.Route] = record.Offset + 1;
    }

    static EventStreamAddress Address(string route)
    {
        try
        {
            return EventStreamAddress.Parse(route);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new SourceFactVerificationException($"The record route cannot be parsed: {exception.Message}");
        }
    }

    DomainEvent Deserialize(ReadOnlyMemory<byte> body)
    {
        try
        {
            return serializer.Deserialize(body);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new SourceFactVerificationException($"The source record cannot be deserialized: {exception.Message}");
        }
    }
}
