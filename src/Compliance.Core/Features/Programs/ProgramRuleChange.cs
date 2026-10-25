using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed record ProgramRuleChange(string Kind, long Revision, ProgramContent? Content,
    Uuid? EditionId, ActorReference Actor, DateTimeOffset ChangedAt);
