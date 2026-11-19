using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Handlers;

/// <summary>Starts one primary designation.</summary>
public sealed class DesignatePlatformPrimaryHandler(PlatformPrimaryOperations operations) : IRequestHandler<DesignatePlatformPrimary>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DesignatePlatformPrimary> context, CancellationToken ct) => operations.DesignateAsync(context, ct);
}
