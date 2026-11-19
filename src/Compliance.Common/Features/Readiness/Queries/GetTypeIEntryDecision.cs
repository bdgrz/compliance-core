using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Readiness.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Queries;

[Discriminator("bdgrz.readiness.type_i_entry.get", 1)]
public sealed record GetTypeIEntryDecision(Uuid TenantId, Uuid ProgramId, Uuid DecisionId)
    : IRequest<TypeIEntryDecisionRecord>, IProgramReadRequest, ICallable;
