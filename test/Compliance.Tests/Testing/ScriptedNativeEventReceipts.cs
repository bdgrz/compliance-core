using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Explicit authenticated-receipt fixture; its clock is supplied independently of occurrence/preparation payloads.</summary>
sealed class ScriptedNativeEventReceipts : INativeEventReceiptReader
{
    readonly Dictionary<(EventStreamAddress, ulong), (DomainEvent Fact, string Sha, DateTimeOffset At)> _values = [];
    public int Reads { get; private set; }
    public void Record(DomainEventRecord source, DateTimeOffset committedAt)
    {
        var at = DateTimeOffset.FromUnixTimeMilliseconds(committedAt.ToUnixTimeMilliseconds());
        _values[(source.Stream, source.ResourceOffset)] = (source.Event,
            ContentDigest.Sha256(source.Event, source.Event.GetType()), at);
    }
    public ValueTask<DateTimeOffset> RecordedAtAsync(DomainEventRecord source, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Reads++;
        if (!_values.TryGetValue((source.Stream, source.ResourceOffset), out var actual))
            throw new ImmutableContentUnavailableException("The scripted broker receipt is absent.");
        if (source.Event.GetType() != actual.Fact.GetType() || source.Event.Metadata != actual.Fact.Metadata ||
            ContentDigest.Sha256(source.Event, source.Event.GetType()) != actual.Sha)
            throw new ImmutableContentIntegrityException("The source differs from the scripted committed tuple.");
        return ValueTask.FromResult(actual.At);
    }
}
