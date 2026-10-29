using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Programs.Models;

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
