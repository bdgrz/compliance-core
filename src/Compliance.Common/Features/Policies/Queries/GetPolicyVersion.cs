using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies.Queries;

[Discriminator("bdgrz.policy.version.get", 1)]
public sealed record GetPolicyVersion(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Version)
    : IRequest<PolicyVersionView>, IProgramReadRequest, ICallable;
