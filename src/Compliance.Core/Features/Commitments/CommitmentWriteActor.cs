using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed record CommitmentWriteActor(Uuid MemberId, string Display)
{
    public ActorReference Snapshot => ActorReference.ForMember(MemberId, Display);
}
