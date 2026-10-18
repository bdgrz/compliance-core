using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>An open commitment preserved with its accountable owner and source engagement.</summary>
public sealed record ProgramRolloverCommitmentContent(
    Uuid CommitmentId,
    string Description,
    Uuid OwnerMemberId,
    DateOnly DueOn,
    Uuid SourceEngagementId,
    Uuid SourceRecordId,
    int SourceRecordVersion,
    string SourceContentSha256);
