using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>A durable, owned management follow-up bound to its exact source record.</summary>
public sealed record ExaminationCommitment(
    Uuid CommitmentId,
    string Description,
    Uuid OwnerMemberId,
    DateOnly DueOn,
    string SourceType,
    Uuid SourceRecordId,
    int SourceRecordVersion,
    string SourceContentSha256);
