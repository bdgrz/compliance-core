using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Programs.Models;

/// <summary>One exact source record's reviewed carry-forward disposition.</summary>
public sealed record ProgramRolloverRecordContent(
    string RecordType,
    Uuid SourceRecordId,
    int SourceVersion,
    string SourceContentSha256,
    string Disposition,
    Uuid? SuccessorRecordId,
    string Rationale);
