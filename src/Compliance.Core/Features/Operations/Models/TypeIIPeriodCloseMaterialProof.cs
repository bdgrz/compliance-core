using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>Complete sealed impact discovery at one close-amendment receipt cut, including exact native source proofs.</summary>
public sealed record TypeIIPeriodCloseMaterialProof(DurableIntentHeader Header, Uuid PeriodId,
    Uuid SupersededCloseSnapshotId, string SupersededCloseSha256, ImmutableContentRef SourcePublication,
    IReadOnlyList<Uuid> ChangedRecordIds, IReadOnlyList<Uuid> AffectedExaminationMaterialIds,
    IReadOnlyList<TypeIIPeriodCloseNativeSource> SourceFacts)
{
    public const string Schema = "type-ii-period-close-material-proof.v1";
}
