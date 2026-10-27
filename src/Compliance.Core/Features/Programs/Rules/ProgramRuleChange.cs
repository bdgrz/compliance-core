using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Rules;

public sealed record ProgramRuleChange(string Kind, long Revision, ProgramContent? Content,
    Uuid? EditionId, ActorReference Actor, DateTimeOffset ChangedAt);
