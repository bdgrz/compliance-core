using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Models;
namespace Bdgrz.Compliance.Features.Incidents.Commands;

/// <summary>
///     Assesses the current incident version. <paramref name="CommandId" /> is the logical identity of the
///     operation; an exact retry replays the original acceptance and appends nothing.
/// </summary>
[Discriminator("bdgrz.incidents.assess-compliance-incident", 2)]
public sealed record AssessComplianceIncident(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid IncidentId, long ExpectedRevision, ComplianceIncidentImpactAssessmentContent Content)
    : IRequest, IRestrictedComplianceRecordMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
