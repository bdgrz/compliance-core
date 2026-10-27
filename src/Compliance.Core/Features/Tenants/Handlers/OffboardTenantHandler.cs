using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class OffboardTenantHandler(TenantLifecycleOperations operations) : IRequestHandler<OffboardTenant, UnitResult>
{
    public async ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<OffboardTenant> context, CancellationToken ct)
    {
        var request = context.Request;
        var result = await operations.ExecuteAsync(context, request.TenantId, request.CommandId, nameof(OffboardTenant),
            request.Reason, request.ExpectedRevision, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result<UnitResult>.Success(new UnitResult()) : Result<UnitResult>.Failure(result.Error!);
    }
}
