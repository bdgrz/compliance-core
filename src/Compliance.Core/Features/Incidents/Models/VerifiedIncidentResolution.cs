using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Incidents.Models;

/// <summary>
///     A resolution whose follow-up completions were verified against their committed native facts. The incident
///     never accepts a work item as complete from a caller-supplied identifier.
/// </summary>
/// <param name="Resolution">The resolution rationale.</param>
/// <param name="ResolvedAt">When the incident was resolved.</param>
/// <param name="EvidenceArtifactIds">The governed evidence artifacts.</param>
/// <param name="Completions">One verified completion per assessed follow-up work item.</param>
public sealed record VerifiedIncidentResolution(string Resolution, DateTimeOffset ResolvedAt,
    IReadOnlyList<Uuid> EvidenceArtifactIds, IReadOnlyList<ComplianceIncidentFollowUpCompletionView> Completions);
