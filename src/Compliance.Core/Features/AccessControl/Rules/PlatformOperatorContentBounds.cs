using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Rules;

/// <summary>Bounds one operator fact's business payload without accumulating operator history.</summary>
static class PlatformOperatorContentBounds
{
    internal const int EventPayloadBytes = 8 * 1024;
    static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static bool ActorFields(ActorAttribution? actor)
    {
        if (actor is null)
            return false;
        var remaining = EventPayloadBytes;
        return Consume(actor.Subject, ref remaining) && Consume(actor.Issuer, ref remaining);
    }

    // Preserve the legacy rule: the trimmed, nonempty reason has at most 500 UTF-16
    // characters. Find its boundaries before allocating a trimmed copy of caller input.
    internal static bool Reason(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var start = 0;
        var end = value.Length;
        while (start < end && char.IsWhiteSpace(value[start]))
            start++;
        while (end > start && char.IsWhiteSpace(value[end - 1]))
            end--;
        if (end - start is not (> 0 and <= 500))
            return false;
        try
        { _ = StrictUtf8.GetByteCount(value.AsSpan(start, end - start)); return true; }
        catch (EncoderFallbackException) { return false; }
    }

    internal static bool CanonicalReason(string? value) => value is { Length: > 0 and <= 500 } &&
        Reason(value) && value == value.Trim();

    // Exact concrete source-generated JSON business payload. The attributed wire
    // envelope is a separate persistence-boundary responsibility.
    internal static bool Payload(DomainEvent value) => DomainEventPayload.Size(value) <= EventPayloadBytes;

    static bool Consume(string? value, ref int remaining)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > remaining)
            return false;
        try
        {
            if (StrictUtf8.GetByteCount(value) > remaining)
                return false;
            var encoded = JsonEncodedText.Encode(value).EncodedUtf8Bytes.Length;
            if (encoded > remaining)
                return false;
            remaining -= encoded;
            return true;
        }
        catch (EncoderFallbackException) { return false; }
    }
}
