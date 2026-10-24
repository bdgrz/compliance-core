using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the TypeIIPeriodCloseAmendedBody domain fact for one tenant-owned subject revision.</summary>
public sealed record TypeIIPeriodCloseAmendedBody(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TypeIIPeriodView State,
    DateTimeOffset OccurredAt)
{
    public ImmutableContentRef? SourceProof { get; init; }
}
