using Bdgrz.Compliance.Features.Policies.Readers;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public static class PolicySource
{
    public static async ValueTask<Result<PolicyReadSnapshot>> ReadAsync(PolicyReader reader,
        Uuid tenantId, Uuid programId, Uuid policyId, CancellationToken ct)
    {
        try
        {
            var policy = await reader.ReadAsync(tenantId, programId, policyId, ct).ConfigureAwait(false);
            return policy.IsVisible ? Result<PolicyReadSnapshot>.Success(policy)
                : Result<PolicyReadSnapshot>.Failure(new RequestError(RequestErrorKind.NotFound, "The policy was not found."));
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<PolicyReadSnapshot>.Failure(PolicyReadFailure.Of(failure));
        }
    }
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
