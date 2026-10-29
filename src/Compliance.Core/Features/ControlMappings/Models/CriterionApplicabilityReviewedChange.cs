using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.ControlMappings.Models;

public sealed record CriterionApplicabilityReviewedChange(Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision,
    int VersionNumber, Uuid ReviewDecisionId, string Outcome, string Rationale, ActorReference Actor,
    DateTimeOffset ReviewedAt, Uuid? SeparationOfDutiesWaiverId, Uuid EditionId, string CriterionIdentifier,
    OperationProvenance Operation)
   ;
