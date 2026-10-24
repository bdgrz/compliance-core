using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

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
