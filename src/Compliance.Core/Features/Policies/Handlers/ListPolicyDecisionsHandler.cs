using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Policies.Queries;
using Bdgrz.Compliance.Features.Policies.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies.Handlers;

public sealed class ListPolicyDecisionsHandler(PolicyReader reader)
    : IRequestHandler<ListPolicyDecisions, Page<PolicyDecisionView>>
{
    public async ValueTask<Result<Page<PolicyDecisionView>>> HandleAsync(
        IRequestContext<ListPolicyDecisions> context, CancellationToken ct)
    {
        var request = context.Request;
        var policy = await PolicySource.ReadAsync(reader, request.TenantId, request.ProgramId,
            request.PolicyId, ct).ConfigureAwait(false);
        return policy.IsSuccess
            ? ControlActivationSource.Paginate(policy.Value.ReadDecisions(), request.Limit,
                request.Cursor, "policy decisions")
            : Result<Page<PolicyDecisionView>>.Failure(policy.Error);
    }
}
