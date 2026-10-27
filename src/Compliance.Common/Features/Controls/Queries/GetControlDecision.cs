using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Controls.Queries;

[Discriminator("bdgrz.control.decision.get", 1)]
public sealed record GetControlDecision(Uuid TenantId, Uuid ProgramId, Uuid ControlId, Uuid DecisionId)
    : IRequest<ControlDecisionView>, IProgramScopedRequest, ICallable;
