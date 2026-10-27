using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.ControlMappings.Models;

public sealed record CriterionNotApplicableWithdrawnChange(Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision,
    int VersionNumber, string Rationale, ActorReference Actor, DateTimeOffset WithdrawnAt, Uuid EditionId,
    string CriterionIdentifier, OperationProvenance Operation);
