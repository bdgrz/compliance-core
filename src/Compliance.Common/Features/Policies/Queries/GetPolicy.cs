using Cntryl.Portia;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Policies.Queries;

[Discriminator("bdgrz.policy.get", 1)]
public sealed record GetPolicy(Uuid TenantId, Uuid ProgramId, Uuid PolicyId)
    : IRequest<PolicyView>, IProgramReadRequest, ICallable;
