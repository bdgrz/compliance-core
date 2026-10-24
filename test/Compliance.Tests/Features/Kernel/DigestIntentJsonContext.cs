using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Tests.Features.Kernel;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(DigestIntent))]
sealed partial class DigestIntentJsonContext : JsonSerializerContext;
