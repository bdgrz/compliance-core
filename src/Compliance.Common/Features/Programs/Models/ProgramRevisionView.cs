using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Programs.Models;

public sealed record ProgramRevisionView(Uuid ProgramId, long Revision, string Name,
    ProgramPlan Plan, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt)
{
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
    public Uuid? CriteriaEditionId { get; init; }
}
