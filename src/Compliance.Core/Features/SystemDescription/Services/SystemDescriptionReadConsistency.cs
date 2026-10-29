using Cntryl.Portia;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.Aggregates.SystemDescription;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.SystemDescription.Services;

/// <summary>Authoritative scoped native/physical source fence; no missing projection is mistaken for absence.</summary>
public sealed class SystemDescriptionReadConsistency(IAggregateReader reader)
{
    public async ValueTask<Result> ConfirmAsync(SystemDescriptionReadSnapshot snapshot, CancellationToken ct)
    {
        var current = await reader.HydrateAsync(new SystemDescriptionAggregate(snapshot.TenantId, snapshot.ProgramId, snapshot.Id), ct).ConfigureAwait(false);
        return current.NativeSequence == snapshot.Owner.NativeSequence && current.CommittedStreamPosition == snapshot.Owner.CommittedStreamPosition
            ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The description source changed while its response was composed. Retry the query.", true));
    }
}
