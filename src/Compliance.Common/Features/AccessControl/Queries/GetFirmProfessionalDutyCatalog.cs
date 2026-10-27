using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.firm.professional-duty.catalog.get", 1)]
public sealed record GetFirmProfessionalDutyCatalog
    : IRequest<FirmProfessionalDutyCatalogView>, IProfessionalDutyAdministrationRequest, ICallable;
