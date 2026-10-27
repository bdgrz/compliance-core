using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
namespace Bdgrz.Compliance.Features.SourceFacts.Projections;

/// <summary>
/// A global area, or one resource inside an area, scanned across every realm. The reader keeps only the requested
/// realm's records; the selector itself never names a realm because Fitz continuation tokens are global-only.
/// </summary>
public sealed record SnapshotSelector(string Area, string? Resource = null)
{
    /// <summary>The global route sent to the broker for this selector.</summary>
    public string Route
    {
        get
        {
            Validate(Area, nameof(Area));
            if (Resource is not null)
                Validate(Resource, nameof(Resource));
            return $"stream://*/{Area}/{Resource ?? "*"}";
        }
    }

    static void Validate(string segment, string name)
    {
        if (string.IsNullOrWhiteSpace(segment) || segment.Contains('/', StringComparison.Ordinal) ||
            segment.Contains('*', StringComparison.Ordinal))
            throw new ArgumentException("A selector segment must be a concrete name.", name);
    }
}
