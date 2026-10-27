using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Examinations.Services;

/// <summary>The source families required for a complete Type I point-in-time baseline.</summary>
public static class TypeIBaselineSourceFamilies
{
    public static IReadOnlyList<string> Required { get; } = Array.AsReadOnly(new[]
    {
        "approved_boundary",
        "workforce",
        "applications",
        "reviewed_systems",
        "system_components",
        "information_assets",
        "data_flows",
        "commitments",
        "system_requirements",
        "criteria",
        "controls",
        "control_criterion_mappings",
        "control_evaluations",
        "policies",
        "policy_communications",
        "access_expectations",
        "access_review_campaigns",
        "risks",
        "providers",
        "evidence_governance",
        "findings",
    });
}
