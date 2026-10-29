using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>One Type II period stream owns the approved-plan binding, close decision, and immutable snapshots.</summary>
public sealed record TypeIIPeriodView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid PeriodId,
    string Status,
    long Revision,
    Uuid ExaminationId,
    Uuid TypeIIPlanId,
    int TypeIIPlanVersion,
    string TypeIIPlanContentSha256,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    Uuid OpeningSnapshotId,
    string OpeningSnapshotSha256,
    IReadOnlyList<ExaminationContentReference> OpeningSourceReferences,
    IReadOnlyList<Uuid> AcknowledgedOpeningGapIds,
    IReadOnlyList<TypeIIPeriodOccurrenceView> ExpectedOccurrences,
    IReadOnlyList<TypeIIPeriodAmendmentView> Amendments,
    TypeIIPeriodCloseValidationView? CloseValidation,
    IReadOnlyList<TypeIIPeriodCloseApprovalView> CloseApprovals,
    IReadOnlyList<TypeIIPeriodCloseSnapshotView> CloseSnapshots,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset UpdatedAt)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableContentRef? OpeningSourceContentManifest { get; init; }
}
