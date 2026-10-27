using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Policies.Queries;
using Bdgrz.Compliance.Features.Policies.Readers;
namespace Bdgrz.Compliance.Features.Policies.Handlers;

/// <summary>Reads the authoritative policy stream, so the record never lags its commands.</summary>
public sealed class GetPolicyHandler(PolicyReader reader, TimeProvider clock)
    : IRequestHandler<GetPolicy, PolicyView>
{
    public async ValueTask<Result<PolicyView>> HandleAsync(IRequestContext<GetPolicy> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var policy = await PolicySource.ReadAsync(reader, request.TenantId, request.ProgramId,
            request.PolicyId, ct).ConfigureAwait(false);
        return policy.IsSuccess
            ? Result<PolicyView>.Success(policy.Value.ToView(PolicySource.Today(clock)))
            : Result<PolicyView>.Failure(policy.Error);
    }
}
