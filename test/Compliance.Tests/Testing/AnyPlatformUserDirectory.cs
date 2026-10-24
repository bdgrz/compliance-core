using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>A platform user directory in which every canonical user exists.</summary>
sealed class AnyPlatformUserDirectory : IPlatformUserDirectoryReader
{
    public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(true);
}
