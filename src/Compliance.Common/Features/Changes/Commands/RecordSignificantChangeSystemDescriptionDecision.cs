using Cntryl.Portia;
using Bdgrz.Compliance.Features.Changes.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Changes.Commands;

/// <summary>Copies one exact approved description or no-change decision into a change record.</summary>
[Discriminator("bdgrz.changes.record-system-description-decision", 3)]
public sealed record RecordSignificantChangeSystemDescriptionDecision(Uuid CommandId, Uuid TenantId,
    Uuid ProgramId, Uuid ChangeId, int ContentVersion, Uuid SystemDescriptionId,
    Uuid SystemDescriptionVersionId, Uuid DecisionId, string Decision, string AssessmentSha256)
    : IRequest<UnitResult>, IIdentifiedRequest, IComplianceImpactReactionRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
