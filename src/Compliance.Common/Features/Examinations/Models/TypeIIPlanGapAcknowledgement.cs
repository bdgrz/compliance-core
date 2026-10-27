using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>An attributable planning acknowledgement for one carried-forward gap.</summary>
public sealed record TypeIIPlanGapAcknowledgement(
    Uuid GapId,
    string Summary,
    string Status,
    string Rationale);
