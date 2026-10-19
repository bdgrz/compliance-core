using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record FindingTypeIBaselineSourceEvent(
    Uuid EventId,
    ulong ProgramStreamVersion,
    long FindingRevision,
    DateTimeOffset OccurredOn,
    string Kind,
    JsonElement Payload);
