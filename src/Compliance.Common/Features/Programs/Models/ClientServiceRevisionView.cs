using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Programs.Models;

public sealed record ClientServiceRevisionView(Uuid ServiceId, long Revision,
    string Name, string Purpose, string OwnerReference, string Status,
    string? RetirementRationale, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ChangedAt, Uuid? ProgramId = null)
{
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
