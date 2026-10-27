using Cntryl.Portia;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Policies.Queries;
namespace Bdgrz.Compliance.Features.Policies.Handlers;

public sealed class PreviewPolicyImpactHandler(PolicyImpactService impact)
    : IRequestHandler<PreviewPolicyImpact, PolicyImpactPreview>
{
    public ValueTask<Result<PolicyImpactPreview>> HandleAsync(
        IRequestContext<PreviewPolicyImpact> context, CancellationToken ct)
    {
        var request = context.Request;
        return impact.PreviewAsync(request.TenantId, request.ProgramId, request.PolicyId,
            request.ExpectedRevision, ct);
    }
}
