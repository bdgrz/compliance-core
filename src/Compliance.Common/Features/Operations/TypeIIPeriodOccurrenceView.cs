using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>A deterministic expected control performance derived from the approved plan.</summary>
public sealed record TypeIIPeriodOccurrenceView(
    Uuid OccurrenceId,
    Uuid ControlId,
    Uuid ControlVersionId,
    int Sequence,
    DateOnly? DueOn,
    Uuid OwnerMemberId,
    string Cadence);
