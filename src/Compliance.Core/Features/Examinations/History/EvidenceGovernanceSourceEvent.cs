using System.Text.Json;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Examinations.History;

sealed record EvidenceGovernanceSourceEvent(
    string Area,
    Uuid AggregateId,
    Uuid EventId,
    ulong AggregateVersion,
    DateTimeOffset OccurredOn,
    string Kind,
    JsonElement Payload);
