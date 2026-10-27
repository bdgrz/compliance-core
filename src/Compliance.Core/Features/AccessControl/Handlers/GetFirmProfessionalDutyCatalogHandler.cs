using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetFirmProfessionalDutyCatalogHandler(IFirmProfessionalDutyDirectoryReader directory)
    : IRequestHandler<GetFirmProfessionalDutyCatalog, FirmProfessionalDutyCatalogView>
{
    public async ValueTask<Result<FirmProfessionalDutyCatalogView>> HandleAsync(
        IRequestContext<GetFirmProfessionalDutyCatalog> context, CancellationToken ct) =>
        Result<FirmProfessionalDutyCatalogView>.Success(await directory.ListAsync(ct).ConfigureAwait(false));
}
