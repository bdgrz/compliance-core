using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Copies one exact approved description or no-change decision into a change record.</summary>
[Discriminator("bdgrz.changes.record-system-description-decision", 2)]
public sealed record RecordSignificantChangeSystemDescriptionDecision(Uuid TenantId,
    Uuid ProgramId, Uuid ChangeId, int ContentVersion, Uuid SystemDescriptionId,
    Uuid SystemDescriptionVersionId, Uuid DecisionId, string Decision, string AssessmentSha256)
    : IRequest, IComplianceImpactReactionRequest;
