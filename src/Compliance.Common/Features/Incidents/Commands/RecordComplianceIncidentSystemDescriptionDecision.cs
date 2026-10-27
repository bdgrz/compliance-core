using Cntryl.Portia;
using Bdgrz.Compliance.Features.Changes.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Incidents.Commands;

/// <summary>
///     Copies an exact period no-change decision into its incident record. <paramref name="CommandId" /> is the
///     logical identity of the reaction effect, so a replayed reaction appends nothing.
/// </summary>
[Discriminator("bdgrz.incidents.record-system-description-decision", 2)]
public sealed record RecordComplianceIncidentSystemDescriptionDecision(Uuid CommandId, Uuid TenantId,
    Uuid ProgramId, Uuid IncidentId, int ContentVersion, Uuid SystemDescriptionId,
    Uuid SystemDescriptionVersionId, Uuid DecisionId, string AssessmentSha256)
    : IRequest, IComplianceImpactReactionRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
