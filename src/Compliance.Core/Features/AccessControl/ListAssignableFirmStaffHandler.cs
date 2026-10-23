using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ListAssignableFirmStaffHandler(IFirmStaffDirectoryReader directory)
    : IRequestHandler<ListAssignableFirmStaff, FirmStaffDirectoryView>
{
    public async ValueTask<Result<FirmStaffDirectoryView>> HandleAsync(
        IRequestContext<ListAssignableFirmStaff> context, CancellationToken ct) =>
        Result<FirmStaffDirectoryView>.Success(await directory.ListAsync(true, ct).ConfigureAwait(false));
}
