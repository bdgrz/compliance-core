using Bdgrz.Compliance.Features.UserIdentities.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Readers;

public interface IEmailAddressDirectoryReader
{
    ValueTask<EmailAddressView?> GetAsync(string emailAddress, CancellationToken ct = default);
    ValueTask<Page<EmailAddressView>> ListAsync(Uuid userId, int? limit, string? cursor,
        CancellationToken ct = default);
}
