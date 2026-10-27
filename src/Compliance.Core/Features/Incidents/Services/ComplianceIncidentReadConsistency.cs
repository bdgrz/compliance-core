using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Projections;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Incidents.Aggregates;
namespace Bdgrz.Compliance.Features.Incidents.Services;

/// <summary>Fences verified scoped details and current restricted authorization against a later owner change.</summary>
public sealed class ComplianceIncidentReadConsistency(IAggregateReader reader)
{
    public async ValueTask<Result> ConfirmAsync(ComplianceIncidentReadSnapshot snapshot, CancellationToken ct)
    {
        var owner = snapshot.Owner;
        var current = await reader.HydrateAsync(new ComplianceIncident(owner.TenantId, owner.ProgramId, owner.Id), ct).ConfigureAwait(false);
        return current.NativeSequence == owner.NativeSequence && current.CommittedStreamPosition == owner.CommittedStreamPosition
            ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The incident changed while its exact authorized source was composed. Retry the query.", true));
    }
}
