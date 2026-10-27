using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetFirmStaffDirectoryHandler(IFirmStaffDirectoryReader directory)
    : IRequestHandler<GetFirmStaffDirectory, FirmStaffDirectoryView>
{
    public async ValueTask<Result<FirmStaffDirectoryView>> HandleAsync(IRequestContext<GetFirmStaffDirectory> context,
        CancellationToken ct) => Result<FirmStaffDirectoryView>.Success(
        await directory.ListAsync(false, ct).ConfigureAwait(false));
}
