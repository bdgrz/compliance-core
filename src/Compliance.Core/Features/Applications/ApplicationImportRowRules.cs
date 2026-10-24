namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Per-field validation of one untrusted staged source row; unchanged from the legacy writer.</summary>
static class ApplicationImportRowRules
{
    public static bool ValidRequest(StageApplicationImport request) =>
        request.SubmissionId != Cntryl.Portia.Uuid.Empty && ValidIdentity(request.SourceKey, 128) &&
        ValidIdentity(request.SourceNamespace, 128) &&
        request.Coverage is "partial" or "declared_complete" &&
        request.Rows is { Count: >= 1 } &&
        request.Rows.All(row => row is not null && row.SourceRecordId is not { Length: > 256 } &&
            row.Name is not { Length: > 200 } && row.Purpose is not { Length: > 2000 } &&
            row.OwnerReference is not { Length: > 2000 });

    public static string[] Findings(ApplicationImportInputRow row, HashSet<string> duplicated)
    {
        var findings = new List<string>();
        if (!ValidIdentity(row.SourceRecordId, 256))
            findings.Add("source_record_id_required");
        if (string.IsNullOrWhiteSpace(row.Name) || row.Name.Length > 200)
            findings.Add("name_required");
        if (string.IsNullOrWhiteSpace(row.Purpose) || row.Purpose.Length > 2000)
            findings.Add("purpose_required");
        if (row.OwnerReference is { Length: > 2000 })
            findings.Add("owner_reference_too_long");
        if (row.SourceRecordId is not null && duplicated.Contains(row.SourceRecordId))
            findings.Add("duplicate_source_record_id");
        return [.. findings];
    }

    public static HashSet<string> Duplicated(IReadOnlyList<ApplicationImportInputRow> rows) =>
        rows.Where(row => !string.IsNullOrWhiteSpace(row.SourceRecordId))
            .GroupBy(row => row.SourceRecordId!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

    public static bool ValidIdentity(string? value, int max) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= max &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
