using Bdgrz.Compliance.Features.Incidents.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

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
