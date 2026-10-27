using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Controls.Projections;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Projections;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Controls.Services;

/// <summary>Resolves retained finding relationships through canonical IDs and the owning control identifier.</summary>
static class ControlImpactRelationships
{
    public static IReadOnlyList<FindingView> Findings(ControlReadSnapshot subject,
        ControlOperationsSnapshot operations, IReadOnlyList<ControlEvaluationView> evaluations,
        FindingReadSnapshot remediation)
    {
        var evaluationIds = evaluations.Select(static evaluation => evaluation.EvaluationId).ToHashSet();
        var deviationIds = evaluations.SelectMany(static evaluation => evaluation.Deviations)
            .Select(static deviation => deviation.DeviationId).ToHashSet();
        return [.. remediation.Findings.Where(finding =>
            finding.Links.Any(MatchesLink) ||
            finding.Source.RecordId is { } sourceId &&
            (finding.Source.Kind is "control_occurrence" or "occurrence_review" &&
             operations.OccurrenceControlId(sourceId) == subject.Id ||
             finding.Source.Kind == "evaluation_deviation" && deviationIds.Contains(sourceId)))];

        bool MatchesLink(FindingLink link)
        {
            if (link.Kind == "control" &&
                ControlDraft.IdFor(subject.TenantId, subject.ProgramId, link.Reference) == subject.Id)
                return true;
            if (!Uuid.TryParse(link.Reference, null, out var id))
                return false;
            return link.Kind switch
            {
                "control" => id == subject.Id,
                "control_occurrence" => operations.OccurrenceControlId(id) == subject.Id,
                "control_evaluation" => evaluationIds.Contains(id),
                _ => false,
            };
        }
    }
}
