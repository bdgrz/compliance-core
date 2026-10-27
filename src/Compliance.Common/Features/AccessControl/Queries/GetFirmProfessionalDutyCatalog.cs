using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.firm.professional-duty.catalog.get", 1)]
public sealed record GetFirmProfessionalDutyCatalog
    : IRequest<FirmProfessionalDutyCatalogView>, IProfessionalDutyAdministrationRequest, ICallable;
