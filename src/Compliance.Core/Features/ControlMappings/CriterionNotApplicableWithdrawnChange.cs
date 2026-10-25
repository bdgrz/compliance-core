using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public sealed record CriterionNotApplicableWithdrawnChange(Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision,
    int VersionNumber, string Rationale, ActorReference Actor, DateTimeOffset WithdrawnAt, Uuid EditionId,
    string CriterionIdentifier, OperationProvenance Operation);
