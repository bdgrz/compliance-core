using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Policies.Queries;
using Bdgrz.Compliance.Features.Policies.Readers;
namespace Bdgrz.Compliance.Features.Policies.Handlers;

public sealed class ListPolicyVersionsHandler(PolicyReader reader)
    : IRequestHandler<ListPolicyVersions, Page<PolicyVersionView>>
{
    public async ValueTask<Result<Page<PolicyVersionView>>> HandleAsync(
        IRequestContext<ListPolicyVersions> context, CancellationToken ct)
    {
        var request = context.Request;
        var policy = await PolicySource.ReadAsync(reader, request.TenantId, request.ProgramId,
            request.PolicyId, ct).ConfigureAwait(false);
        return policy.IsSuccess
            ? ControlActivationSource.Paginate(policy.Value.ReadVersions(), request.Limit,
                request.Cursor, "policy versions")
            : Result<Page<PolicyVersionView>>.Failure(policy.Error);
    }
}
