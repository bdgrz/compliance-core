using Cntryl.Portia;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Policies.Queries;

[Discriminator("bdgrz.policy.impact.preview", 1)]
public sealed record PreviewPolicyImpact(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long ExpectedRevision)
    : IRequest<PolicyImpactPreview>, IProgramReadRequest, ICallable;
