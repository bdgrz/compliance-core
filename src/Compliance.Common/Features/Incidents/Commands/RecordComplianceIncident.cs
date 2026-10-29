using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Incidents.Models;
namespace Bdgrz.Compliance.Features.Incidents.Commands;

/// <summary>Records the record compliance incident intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.incidents.record-compliance-incident", 2)]
public sealed record RecordComplianceIncident(Uuid TenantId, Uuid ProgramId, Uuid IncidentId, ComplianceIncidentContent Content) : IRequest, IRestrictedComplianceRecordMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => IncidentId;
}
