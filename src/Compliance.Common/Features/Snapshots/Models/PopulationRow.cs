using System.Text.Json;

namespace Bdgrz.Compliance.Features.Snapshots.Models;

public sealed record PopulationRow(string Key, JsonElement Content);
