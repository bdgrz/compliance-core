using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.UserIdentities.Readers;

/// <summary>Current presentation from authenticated provider facts; not an authorization source.</summary>
public interface IUserDisplayNameReader
{
    ValueTask<string?> ReadAsync(Uuid userId, CancellationToken ct = default);
}
