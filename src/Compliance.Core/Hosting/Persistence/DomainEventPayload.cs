using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
namespace Bdgrz.Compliance.Hosting.Persistence;

/// <summary>Measures concrete business payloads; the store checks attributed envelopes separately.</summary>
static class DomainEventPayload
{
    public const int MaximumBytes = 48 * 1024;

    public static int Size(DomainEvent ev)
    {
        ArgumentNullException.ThrowIfNull(ev);
        var metadata = ComplianceCoreJsonContext.Default.GetTypeInfo(ev.GetType())
            ?? throw new InvalidOperationException("A domain event requires concrete generated JSON metadata.");
        return JsonSerializer.SerializeToUtf8Bytes(ev, metadata).Length;
    }
}
