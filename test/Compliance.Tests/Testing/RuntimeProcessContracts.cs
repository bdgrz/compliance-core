using System.Text.Json;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>The checked-in compatibility contract for registered runtime processes.</summary>
static class RuntimeProcessContracts
{
    public static JsonDocument Read() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "process-contracts.json")));
}
