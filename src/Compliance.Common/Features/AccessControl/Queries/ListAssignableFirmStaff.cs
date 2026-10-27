using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.firm-staff.assignable.list", 1)]
public sealed record ListAssignableFirmStaff(Uuid TenantId)
    : IRequest<FirmStaffDirectoryView>, IIndependenceAdministrationRequest, ICallable;
