using Cntryl.Portia;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Readers;

public interface IEmailAddressDirectoryReader
{
    ValueTask<EmailAddressView?> GetAsync(string emailAddress, CancellationToken ct = default);
    ValueTask<Page<EmailAddressView>> ListAsync(Uuid userId, int? limit, string? cursor,
        CancellationToken ct = default);
}
