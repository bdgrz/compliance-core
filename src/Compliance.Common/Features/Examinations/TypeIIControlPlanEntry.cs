using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>An exact control version, accountable owner, cadence, and expected occurrence count.</summary>
public sealed record TypeIIControlPlanEntry(
    Uuid ControlId,
    Uuid ControlVersionId,
    string ControlTitle,
    Uuid? OwnerMemberId,
    string Cadence,
    int ExpectedOccurrences,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveThrough);
