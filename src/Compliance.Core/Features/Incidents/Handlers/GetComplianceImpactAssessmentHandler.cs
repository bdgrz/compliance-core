using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Changes.Services;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.Incidents.Queries;
using Bdgrz.Compliance.Features.Incidents.Readers;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Features.Incidents.Handlers;

public sealed class GetComplianceImpactAssessmentHandler(ComplianceIncidentReader incidents, RestrictedComplianceRecordVisibility visibility,
    ComplianceIncidentReadConsistency consistency) : IRequestHandler<GetComplianceImpactAssessment, ComplianceIncidentImpactAssessmentView>
{
    public async ValueTask<Result<ComplianceIncidentImpactAssessmentView>> HandleAsync(IRequestContext<GetComplianceImpactAssessment> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.IncidentId == Uuid.Empty)
            return Result<ComplianceIncidentImpactAssessmentView>.Failure(new RequestError(RequestErrorKind.Validation, "Tenant, program and incident identities are required."));
        try
        {
            var snapshot = await incidents.ReadAsync(request.TenantId, request.ProgramId, request.IncidentId, ct).ConfigureAwait(false);
            if (snapshot.View is not { Current: { } version, CurrentAssessment: { } assessment } view ||
                !await visibility.CanReadAsync(request.TenantId, context.Actor, version.Content.HandlingClass,
                    view.Assessments.Select(item => item.AssessedByMemberId).ToArray(), ct).ConfigureAwait(false))
                return Result<ComplianceIncidentImpactAssessmentView>.Failure(new RequestError(RequestErrorKind.NotFound, "The current compliance-incident assessment was not found."));
            var confirmed = await consistency.ConfirmAsync(snapshot, ct).ConfigureAwait(false);
            return confirmed.IsSuccess ? Result<ComplianceIncidentImpactAssessmentView>.Success(assessment) : Result<ComplianceIncidentImpactAssessmentView>.Failure(confirmed.Error!);
        }
        catch (ImmutableContentIntegrityException exception)
        { return Result<ComplianceIncidentImpactAssessmentView>.Failure(IncidentOperations.ContentFailure(exception)); }
    }
}
