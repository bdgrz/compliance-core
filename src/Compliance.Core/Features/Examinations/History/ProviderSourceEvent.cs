using System.Text.Json;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Examinations.History;

sealed record ProviderSourceEvent(string Area, Uuid AggregateId, Uuid EventId,
    ulong AggregateVersion, DateTimeOffset RecordedAt, string Kind, JsonElement Content);
