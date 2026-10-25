using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public sealed record CriterionApplicabilityReviewedChange(Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision,
    int VersionNumber, Uuid ReviewDecisionId, string Outcome, string Rationale, ActorReference Actor,
    DateTimeOffset ReviewedAt, Uuid? SeparationOfDutiesWaiverId, Uuid EditionId, string CriterionIdentifier,
    OperationProvenance Operation)
   ;
