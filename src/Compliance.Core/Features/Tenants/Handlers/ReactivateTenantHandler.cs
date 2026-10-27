using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Tenants.Commands;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class ReactivateTenantHandler(TenantLifecycleOperations operations) : IRequestHandler<ReactivateTenant, UnitResult>
{
    public async ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<ReactivateTenant> context, CancellationToken ct)
    {
        var request = context.Request;
        var result = await operations.ExecuteAsync(context, request.TenantId, request.CommandId, nameof(ReactivateTenant),
            request.Reason, null, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result<UnitResult>.Success(new UnitResult()) : Result<UnitResult>.Failure(result.Error!);
    }
}
