using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Queries;

[Discriminator("bdgrz.boundary.decision.get", 1)]
public sealed record GetBoundaryDecision(Uuid TenantId, Uuid BoundaryId, Uuid DecisionId)
    : IRequest<BoundaryDecisionView>, IBoundaryProgramReadRequest, ICallable;
