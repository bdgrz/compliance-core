using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed record ControlWriteActor(Uuid MemberId, string Display)
{
    public ActorReference Snapshot => ActorReference.ForMember(MemberId, Display);
}
