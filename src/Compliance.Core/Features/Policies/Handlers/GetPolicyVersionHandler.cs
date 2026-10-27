using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Policies.Queries;
using Bdgrz.Compliance.Features.Policies.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies.Handlers;

public sealed class GetPolicyVersionHandler(PolicyReader reader)
    : IRequestHandler<GetPolicyVersion, PolicyVersionView>
{
    public async ValueTask<Result<PolicyVersionView>> HandleAsync(
        IRequestContext<GetPolicyVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        var policy = await PolicySource.ReadAsync(reader, request.TenantId, request.ProgramId,
            request.PolicyId, ct).ConfigureAwait(false);
        if (!policy.IsSuccess)
            return Result<PolicyVersionView>.Failure(policy.Error);
        return policy.Value.FindVersion(request.Version) is { } version
            ? Result<PolicyVersionView>.Success(version)
            : Result<PolicyVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The policy version was not found."));
    }
}
