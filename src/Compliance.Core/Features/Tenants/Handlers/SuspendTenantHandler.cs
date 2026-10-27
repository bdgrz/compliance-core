using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class SuspendTenantHandler(TenantLifecycleOperations operations) : IRequestHandler<SuspendTenant, UnitResult>
{
    public async ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<SuspendTenant> context, CancellationToken ct)
    {
        var request = context.Request;
        var result = await operations.ExecuteAsync(context, request.TenantId, request.CommandId, nameof(SuspendTenant),
            request.Reason, request.ExpectedRevision, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result<UnitResult>.Success(new UnitResult()) : Result<UnitResult>.Failure(result.Error!);
    }
}
