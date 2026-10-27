using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Programs.Models;

public sealed record ProductClientServiceChange(string Kind, long Revision, Uuid ProgramId,
    ProductClientServiceContent? Content, string? Rationale, ActorReference Actor, DateTimeOffset ChangedAt);
