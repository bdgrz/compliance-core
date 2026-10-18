using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Reconciliation facts supplied by close validation over exact source versions.</summary>
public sealed record TypeIIPeriodCloseValidationContent(
    IReadOnlyList<ExaminationContentReference> SourceReferences,
    IReadOnlyList<Uuid> ExpectedOccurrenceIds,
    IReadOnlyList<Uuid> ActualOccurrenceIds,
    IReadOnlyList<Uuid> DuplicateOccurrenceIds,
    IReadOnlyList<Uuid> MissingWorkIds,
    IReadOnlyList<Uuid> UnresolvedMaterialItemIds);
