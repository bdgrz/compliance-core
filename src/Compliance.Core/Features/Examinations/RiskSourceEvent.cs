using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record RiskSourceEvent(string Area, Uuid AggregateId, Uuid EventId,
    ulong AggregateVersion, DateTimeOffset RecordedAt, string Kind, JsonElement Content);
