using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Readers;

/// <summary>Current presentation from authenticated provider facts; not an authorization source.</summary>
public interface IUserDisplayNameReader
{
    ValueTask<string?> ReadAsync(Uuid userId, CancellationToken ct = default);
}
