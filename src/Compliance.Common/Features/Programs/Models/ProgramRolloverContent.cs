using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Models;

/// <summary>Exact source engagement and selected, reviewable inputs for the next period.</summary>
public sealed record ProgramRolloverContent(
    Uuid SourcePeriodId,
    Uuid SourceExaminationId,
    Uuid SourceCloseSnapshotId,
    string SourceCloseSnapshotSha256,
    Uuid AuditOutcomeId,
    string AuditOutcomeContentSha256,
    DateOnly NextPeriodStart,
    DateOnly NextPeriodEnd,
    Uuid NextPeriodId,
    Uuid NextTypeIIPlanId,
    IReadOnlyList<ProgramRolloverRecordContent> Records,
    IReadOnlyList<ProgramRolloverCommitmentContent> Commitments,
    IReadOnlyList<string> ComparisonMeasures)
{
    public string ProposalRationale { get; init; } = string.Empty;
}
