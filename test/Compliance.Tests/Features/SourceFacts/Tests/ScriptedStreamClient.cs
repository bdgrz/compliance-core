using Cntryl.Fitz;
using Cntryl.Portia;
using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;

/// <summary>
/// Scripted Fitz stream client that yields bounded replay batches through the captured tail.
/// </summary>
sealed class ScriptedStreamClient : IStreamClient
{
    readonly List<Stored> _records = [];

    public int PageSize { get; init; } = 100;
    public int BatchesProduced { get; private set; }
    public List<Request> Requests { get; } = [];
    public int Total => _records.Count;

    public void Append(EventStreamAddress stream, ReadOnlyMemory<byte> body, bool filtered = false, ulong? skipResource = null)
    {
        var route = stream.ToString();
        var resource = skipResource ?? (ulong)_records.Count(record => record.Route == route);
        var realm = (ulong)_records.Count(record => record.Address.Realm == stream.Realm);
        _records.Add(new Stored(route, stream, resource, realm, (ulong)_records.Count, body.ToArray(), filtered));
    }

    public async IAsyncEnumerable<StreamReadBatch> ReadAsync(string route, ulong startOffset,
        StreamReadMode mode, StreamReadOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (mode != StreamReadMode.Replay)
            throw new InvalidOperationException("The scripted source client only supports snapshot replay.");
        if (!route.StartsWith("stream://", StringComparison.Ordinal))
            throw new InvalidOperationException("A stream selector must use its canonical scheme.");
        options ??= new StreamReadOptions();
        var snapshot = (ulong)_records.Count;
        var batchSize = Math.Min(options.BatchSize, (ulong)PageSize);
        Requests.Add(new Request(route, startOffset, mode, batchSize));
        var next = startOffset;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var matching = _records.Where(record => record.GlobalOffset >= next && record.GlobalOffset < snapshot &&
                Matches(route, record)).ToList();
            var take = matching.Take((int)Math.Min(batchSize, int.MaxValue)).ToList();
            var items = take.Select(record => record.Filtered
                ? new StreamReadItem(record.Route, StreamReadItemKind.Filtered, null, record.ResourceOffset, 0, 0,
                    StreamFilteredReason.Permission)
                : new StreamReadItem(record.Route, StreamReadItemKind.Event,
                    new StreamRecord(record.Route, record.ResourceOffset, null, record.RealmOffset, record.GlobalOffset,
                        record.Body, null, 0))).ToArray();
            var nextOffset = take.Count == 0 ? next : checked(take[^1].GlobalOffset + 1);
            var caughtUp = matching.Count == take.Count;
            BatchesProduced++;
            yield return new StreamReadBatch(items, next, nextOffset, caughtUp);
            if (caughtUp)
                yield break;
            if (nextOffset <= next)
                throw new InvalidOperationException("The scripted source client made no replay progress.");
            next = nextOffset;
        }
    }

    static bool Matches(string route, Stored record)
    {
        if (route == "stream://**")
            return true;
        var parts = route["stream://".Length..].Split('/');
        return parts[1] == record.Address.Area && (parts[2] == "*" || parts[2] == record.Address.Resource);
    }

    public Task<IStreamSession> BeginAsync(string route, ReadOnlyMemory<byte>? ingestMetadata = null,
        CancellationToken ct = default) => throw new NotSupportedException();
    public Task<StreamRecord?> PeekAsync(string route, CancellationToken ct = default) =>
        throw new NotSupportedException();
    public Task<StreamMetadata> MetadataAsync(string route, CancellationToken ct = default) =>
        throw new NotSupportedException();
    public Task<StreamSubscription> SubscribeAsync(string pattern, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public sealed record Request(string Route, ulong Start, StreamReadMode Mode, ulong BatchSize);

    sealed record Stored(string Route, EventStreamAddress Address, ulong ResourceOffset, ulong RealmOffset,
        ulong GlobalOffset, byte[] Body, bool Filtered);
}
