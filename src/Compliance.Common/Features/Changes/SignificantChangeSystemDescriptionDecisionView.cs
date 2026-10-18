using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>An independent disposition of description review by exact successor or no-change decision.</summary>
public sealed record SignificantChangeSystemDescriptionDecisionView(
    int ContentVersion,
    string Decision,
    Uuid DecisionId,
    string Rationale,
    Uuid DecidedByMemberId,
    string DecidedByDisplay,
    DateTimeOffset DecidedAt);
