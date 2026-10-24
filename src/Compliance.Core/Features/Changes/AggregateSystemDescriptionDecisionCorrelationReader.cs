using Bdgrz.Compliance.Features.Incidents;
using Cntryl.Portia;
using IncidentAggregate = Bdgrz.Compliance.Features.Incidents.ComplianceIncident;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Reads exact current change and incident assessment facts from their owning aggregates.</summary>
public sealed class AggregateSystemDescriptionDecisionCorrelationReader(IAggregateReader reader, SignificantChangeReader changes)
    : ISystemDescriptionDecisionCorrelationReader
{
    public async ValueTask<ComplianceImpactAssessmentCorrelation?> GetCurrentChangeAsync(
        Uuid tenantId, Uuid programId, Uuid changeId, CancellationToken ct = default)
    {
        var change = await changes.ReadAsync(tenantId, programId, changeId, ct)
            .ConfigureAwait(false);
        if (change.View is not { CurrentAssessment: { } assessment } current ||
            current.TenantId != tenantId || current.ProgramId != programId ||
            current.SignificantChangeId != changeId ||
            assessment.ContentVersion != current.CurrentVersionNumber)
            return null;
        return new ComplianceImpactAssessmentCorrelation(tenantId, programId, changeId,
            assessment.ContentVersion, SignificantChange.AssessmentSha256(assessment),
            assessment.AssessedAt, assessment.RequiresSystemDescriptionReview);
    }

    public async ValueTask<ComplianceImpactAssessmentCorrelation?> GetCurrentIncidentAsync(
        Uuid tenantId, Uuid programId, Uuid incidentId, CancellationToken ct = default)
    {
        var incident = await reader.HydrateAsync(new IncidentAggregate(tenantId, programId, incidentId), ct)
            .ConfigureAwait(false);
        if (incident.Current is not { CurrentAssessment: { } assessment } current ||
            current.TenantId != tenantId || current.ProgramId != programId ||
            current.ComplianceIncidentId != incidentId ||
            assessment.ContentVersion != current.CurrentVersionNumber)
            return null;
        return new ComplianceImpactAssessmentCorrelation(tenantId, programId, incidentId,
            assessment.ContentVersion, IncidentAggregate.AssessmentSha256(assessment),
            assessment.AssessedAt, assessment.RequiresSystemDescriptionReview);
    }
}
