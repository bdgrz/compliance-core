using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Complete sealed impact discovery at one close-amendment receipt cut, including exact native source proofs.</summary>
public sealed record TypeIIPeriodCloseMaterialProof(DurableIntentHeader Header, Uuid PeriodId,
    Uuid SupersededCloseSnapshotId, string SupersededCloseSha256, ImmutableContentRef SourcePublication,
    IReadOnlyList<Uuid> ChangedRecordIds, IReadOnlyList<Uuid> AffectedExaminationMaterialIds,
    IReadOnlyList<TypeIIPeriodCloseNativeSource> SourceFacts)
{
    public const string Schema = "type-ii-period-close-material-proof.v1";
}
