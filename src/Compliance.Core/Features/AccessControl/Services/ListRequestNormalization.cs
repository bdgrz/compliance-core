using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>
///     Normalizes the raw <c>Search</c>/<c>Sort</c> members shared by every list request. Each
///     directory has exactly one sortable index today, so <c>Sort</c> only ever toggles direction.
/// </summary>
static class ListRequestNormalization
{
    public static string? NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    public static bool IsDescending(string? sort) =>
        sort is not null && sort.Contains("desc", StringComparison.OrdinalIgnoreCase);
}
