using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Tenants.Contracts;

public interface IPlatformOperatorAccess
{
    ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default);
}
