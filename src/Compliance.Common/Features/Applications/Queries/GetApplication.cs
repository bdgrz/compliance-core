using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.application.get", 1)]
public sealed record GetApplication(Uuid TenantId, Uuid ApplicationId,
    long? MinimumRevision = null)
    : IRequest<ApplicationView>, IRestrictedApplicationResourceReadRequest, ICallable;
