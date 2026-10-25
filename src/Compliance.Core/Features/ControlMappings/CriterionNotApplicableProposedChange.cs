using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public sealed record CriterionNotApplicableProposedChange(Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision,
    int VersionNumber, Uuid EditionId, string CriterionIdentifier, string Rationale, Uuid ProposerMemberId,
    ActorReference Actor, DateTimeOffset ProposedAt, OperationProvenance Operation);
