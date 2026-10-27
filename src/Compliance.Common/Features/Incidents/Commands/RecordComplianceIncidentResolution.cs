using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>
///     Resolves the incident. <paramref name="CommandId" /> is the logical identity of the operation; completion of
///     the assessed follow-up work is proven by the native facts named in the content, never asserted by id.
/// </summary>
[Discriminator("bdgrz.incidents.record-compliance-incident-resolution", 2)]
public sealed record RecordComplianceIncidentResolution(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid IncidentId, long ExpectedRevision, ComplianceIncidentResolutionContent Content)
    : IRequest, IRestrictedComplianceRecordMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
