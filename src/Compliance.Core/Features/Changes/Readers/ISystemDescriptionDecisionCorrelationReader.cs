using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.Changes.Readers;

public sealed record ComplianceImpactAssessmentCorrelation(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid RecordId,
    int ContentVersion,
    string AssessmentSha256,
    DateTimeOffset AssessedAt,
    bool RequiresSystemDescriptionReview);

/// <summary>Reads an exact current assessment for system-description decision correlation.</summary>
public interface ISystemDescriptionDecisionCorrelationReader
{
    ValueTask<ComplianceImpactAssessmentCorrelation?> GetCurrentChangeAsync(Uuid tenantId,
        Uuid programId, Uuid changeId, CancellationToken ct = default);

    ValueTask<ComplianceImpactAssessmentCorrelation?> GetCurrentIncidentAsync(Uuid tenantId,
        Uuid programId, Uuid incidentId, CancellationToken ct = default);
}
