using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Handlers;

/// <summary>Starts one replacement-first primary release.</summary>
public sealed class ReleasePlatformPrimaryHandler(PlatformPrimaryOperations operations) : IRequestHandler<ReleasePlatformPrimary>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReleasePlatformPrimary> context, CancellationToken ct) => operations.ReleaseAsync(context, ct);
}
