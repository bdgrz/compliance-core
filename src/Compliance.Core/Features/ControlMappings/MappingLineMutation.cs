using System.Text.Json;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>Complete authored intent, business change and bounded current authority of one native transition.</summary>
public sealed record MappingLineMutation(JsonElement Authored, JsonElement Change, JsonElement Current);
