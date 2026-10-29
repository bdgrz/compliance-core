using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.firm-staff.list", 1)]
public sealed record GetFirmStaffDirectory
    : IRequest<FirmStaffDirectoryView>, IFirmStaffAdministrationRequest, ICallable;
