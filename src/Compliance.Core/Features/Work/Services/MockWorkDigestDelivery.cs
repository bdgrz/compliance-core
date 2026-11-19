using System.Collections.Concurrent;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Services;

sealed class MockWorkDigestDelivery : IWorkDigestDelivery
{
    readonly ConcurrentDictionary<Cntryl.Portia.Uuid, WorkDigestDeliveryMessage> _messages = new();

    public ValueTask<WorkDigestTransportOutcome> SendAsync(Cntryl.Portia.Uuid tenantId,
        WorkDigestDeliveryMessage message, CancellationToken ct)
    {
        _ = tenantId;
        if (ct.IsCancellationRequested)
            return ValueTask.FromResult(new WorkDigestTransportOutcome(
                WorkDigestTransportOutcomeKind.Unknown, "transport_ambiguous"));
        _messages.TryAdd(message.MessageId, message);
        return ValueTask.FromResult(new WorkDigestTransportOutcome(
            WorkDigestTransportOutcomeKind.Accepted));
    }

    internal bool TryGet(Cntryl.Portia.Uuid messageId, out WorkDigestDeliveryMessage? message) =>
        _messages.TryGetValue(messageId, out message);
}
