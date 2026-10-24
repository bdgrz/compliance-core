using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryWriteActor(Uuid MemberId, string Display)
{
    public ActorReference Snapshot => ActorReference.ForMember(MemberId, Display);
}
