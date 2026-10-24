using System.Text.Json;
using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Exact native payload bytes and their authoritative stream/event/version identity in a close source.</summary>
public sealed record TypeIIPeriodCloseNativeSource(NativeFactReference Fact, string EventKind, string PayloadSha256,
    JsonElement Payload);
