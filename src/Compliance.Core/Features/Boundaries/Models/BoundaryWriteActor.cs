using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundaryWriteActor(Uuid MemberId, string Display)
{
    public ActorReference Snapshot => ActorReference.ForMember(MemberId, Display);
}
