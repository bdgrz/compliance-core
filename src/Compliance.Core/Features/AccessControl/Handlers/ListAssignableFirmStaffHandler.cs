using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class ListAssignableFirmStaffHandler(IFirmStaffDirectoryReader directory)
    : IRequestHandler<ListAssignableFirmStaff, FirmStaffDirectoryView>
{
    public async ValueTask<Result<FirmStaffDirectoryView>> HandleAsync(
        IRequestContext<ListAssignableFirmStaff> context, CancellationToken ct) =>
        Result<FirmStaffDirectoryView>.Success(await directory.ListAsync(true, ct).ConfigureAwait(false));
}
