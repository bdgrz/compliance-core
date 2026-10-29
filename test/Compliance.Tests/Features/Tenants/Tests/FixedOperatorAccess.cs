using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Contracts;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

sealed class FixedOperatorAccess(params Uuid[] userIds) : IPlatformOperatorAccess
{
    public bool AllowAll { get; init; }

    public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) =>
        ValueTask.FromResult(AllowAll || userIds.Contains(userId));
}
