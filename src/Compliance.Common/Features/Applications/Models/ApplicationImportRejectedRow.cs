using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Models;

public sealed record ApplicationImportRejectedRow(Uuid RowId, int RowNumber, string? SourceRecordId,
    IReadOnlyList<string> Findings);
