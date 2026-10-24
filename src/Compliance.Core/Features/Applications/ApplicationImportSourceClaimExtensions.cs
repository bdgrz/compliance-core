namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationImportSourceClaimExtensions
{
    /// <summary>The governed observation fields a staged row would change in the published observation.</summary>
    public static IReadOnlyList<string> ChangedFields(this ApplicationImportSourceClaimRecord claim,
        ApplicationImportStagedRow row)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(row);
        var changed = new List<string>();
        if (claim.Observation.Name.Trim() != row.Name?.Trim())
            changed.Add("name");
        if (claim.Observation.Purpose.Trim() != row.Purpose?.Trim())
            changed.Add("purpose");
        if (claim.Observation.OwnerReference?.Trim() != row.OwnerReference?.Trim())
            changed.Add("owner_reference");
        return changed.AsReadOnly();
    }
}
