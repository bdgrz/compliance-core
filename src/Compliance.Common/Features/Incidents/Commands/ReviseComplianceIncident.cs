using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Models;
namespace Bdgrz.Compliance.Features.Incidents.Commands;

/// <summary>
///     Revises the incident content. <paramref name="CommandId" /> is the logical identity of the operation; an
///     exact retry replays the original acceptance and appends nothing.
/// </summary>
[Discriminator("bdgrz.incidents.revise-compliance-incident", 2)]
public sealed record ReviseComplianceIncident(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid IncidentId, long ExpectedRevision, ComplianceIncidentContent Content, string ChangeReason)
    : IRequest, IRestrictedComplianceRecordMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
