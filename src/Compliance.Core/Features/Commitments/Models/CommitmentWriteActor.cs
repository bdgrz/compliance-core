using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Commitments.Models;

public sealed record CommitmentWriteActor(Uuid MemberId, string Display)
{
    public ActorReference Snapshot => ActorReference.ForMember(MemberId, Display);
}
