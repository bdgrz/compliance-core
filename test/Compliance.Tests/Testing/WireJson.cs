using System.Text.Json;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>The wire form of a contract value, for equality of values whose collections are separate instances.</summary>
static class WireJson
{
    public static string Of<T>(T value) => JsonSerializer.Serialize(value,
        ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!);
}
