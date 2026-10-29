using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Operations.Services;

/// <summary>Reads complete scalar command authority; immutable body availability does not erase an accepted lifecycle decision.</summary>
static class ObservationPeriodRead
{
    public static async ValueTask<Result<ObservationPeriodView>> GetAsync(IAggregateReader reader, Uuid tenantId,
        Uuid programId, Uuid periodId, CancellationToken ct)
    {
        var owner = await reader.HydrateAsync(new TypeIIPeriod(tenantId, programId, periodId), ct).ConfigureAwait(false);
        return owner.Header is { } header && header.TenantId == tenantId && header.ProgramId == programId && header.PeriodId == periodId
            ? Result<ObservationPeriodView>.Success(new ObservationPeriodView(tenantId, programId, periodId, header.ExaminationId,
                header.TypeIIPlanId, header.TypeIIPlanVersion, header.Status, header.Revision, header.PeriodStart, header.PeriodEnd,
                header.ExpectedOccurrenceCount, owner.Pending is TypeIIPeriodAmendmentAccepted ? owner.PendingOperationId : null,
                header.ValidationStatus, header.LatestCloseSnapshotId, header.LatestCloseVersion, header.UpdatedAt))
            : Result<ObservationPeriodView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The observation period has not opened in this program."));
    }
}
