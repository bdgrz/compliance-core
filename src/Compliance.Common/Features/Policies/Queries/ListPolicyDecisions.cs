using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies.Queries;

[Discriminator("bdgrz.policy.decisions.list", 1)]
public sealed record ListPolicyDecisions(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<PolicyDecisionView>>, IProgramReadRequest, ICallable;
