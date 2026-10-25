using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed record ProductClientServiceChange(string Kind, long Revision, Uuid ProgramId,
    ProductClientServiceContent? Content, string? Rationale, ActorReference Actor, DateTimeOffset ChangedAt);
