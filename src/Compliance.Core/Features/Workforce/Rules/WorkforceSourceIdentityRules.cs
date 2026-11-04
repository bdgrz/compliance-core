using Bdgrz.Compliance.Features.Workforce.Models;

namespace Bdgrz.Compliance.Features.Workforce.Rules;

/// <summary>Canonical source identity normalization and limits shared by workforce facts.</summary>
static class WorkforceSourceIdentityRules
{
    public static WorkforceSourceIdentity Normalize(WorkforceSourceIdentity source) => source with
    {
        SourceKind = source.SourceKind?.Trim() ?? string.Empty,
        SourceSystem = source.SourceSystem?.Trim() ?? string.Empty,
        SourceRecordId = source.SourceRecordId?.Trim() ?? string.Empty,
        SourceRevision = source.SourceRevision?.Trim() ?? string.Empty,
    };

    public static string? InputError(WorkforceSourceIdentity source) =>
        source.SourceSystem.Length is 0 or > 200 || source.SourceRecordId.Length is 0 or > 500 ||
        source.SourceRevision.Length is 0 or > 200
            ? "A source requires a system (1–200), record ID (1–500), and revision (1–200 characters)."
            : null;
}
