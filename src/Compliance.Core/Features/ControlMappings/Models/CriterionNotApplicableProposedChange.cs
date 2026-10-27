using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.ControlMappings.Models;

public sealed record CriterionNotApplicableProposedChange(Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision,
    int VersionNumber, Uuid EditionId, string CriterionIdentifier, string Rationale, Uuid ProposerMemberId,
    ActorReference Actor, DateTimeOffset ProposedAt, OperationProvenance Operation);
