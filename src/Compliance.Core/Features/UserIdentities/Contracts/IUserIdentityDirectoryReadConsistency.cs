using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.UserIdentities.Contracts;

public interface IUserIdentityDirectoryReadConsistency
{
    ValueTask<Result> EnsureCaughtUpAsync(CancellationToken ct);
}
