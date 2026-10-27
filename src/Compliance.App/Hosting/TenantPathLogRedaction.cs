using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Hosting;

/// <summary>
/// Keeps request paths, which can carry a client-identifying organization slug, out of logs and
/// log scopes. Telemetry identifies an organization only by its opaque tenant ID (ADR 0009).
/// </summary>
static class TenantPathLogRedaction
{
    const string AspNetCore = "Microsoft.AspNetCore";

    // Request start/finish events and the RequestPath scope. The scope exists whenever this
    // category is enabled at any level, so the category is disabled outright.
    const string HostingDiagnostics = "Microsoft.AspNetCore.Hosting.Diagnostics";

    public static IServiceCollection AddTenantPathLogRedaction(this IServiceCollection services) =>
        services.PostConfigure<LoggerFilterOptions>(Apply);

    /// <summary>
    /// Runs after configuration binding. Every configured rule that could select a
    /// <c>Microsoft.AspNetCore</c> category ahead of the rules added here is removed, so neither a
    /// category, a differently cased, a wildcard, nor a provider-specific override can re-enable
    /// path-bearing framework diagnostics below <see cref="LogLevel.Warning"/>.
    /// </summary>
    internal static void Apply(LoggerFilterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // A rule is added for every provider named in configuration because provider-specific
        // rules, including a provider's Default, take precedence over provider-neutral rules.
        var providers = options.Rules.Select(rule => rule.ProviderName)
            .Append(null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // The logger selects the matching rule with the longest category name, and the last one
        // on a tie. A shorter matching rule already loses to the rules appended below.
        for (var index = options.Rules.Count - 1; index >= 0; index--)
        {
            if (CouldOutrankRedaction(options.Rules[index].CategoryName))
                options.Rules.RemoveAt(index);
        }

        foreach (var provider in providers)
        {
            options.Rules.Add(new LoggerFilterRule(provider, AspNetCore, LogLevel.Warning, null));
            options.Rules.Add(new LoggerFilterRule(provider, HostingDiagnostics, LogLevel.None, null));
        }
    }

    static bool CouldOutrankRedaction(string? category)
    {
        if (category is null || category.Length < AspNetCore.Length)
            return false;

        // Category rules match case-insensitively and may contain one '*' wildcard.
        var wildcard = category.IndexOf('*', StringComparison.Ordinal);
        var prefix = wildcard < 0 ? category : category[..wildcard];
        return prefix.StartsWith(AspNetCore, StringComparison.OrdinalIgnoreCase) ||
            AspNetCore.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
