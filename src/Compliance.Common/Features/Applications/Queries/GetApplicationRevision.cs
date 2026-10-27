using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.application.revision.get", 1)]
public sealed record GetApplicationRevision(Uuid TenantId, Uuid ApplicationId, long Revision)
    : IRequest<ApplicationRevisionView>, IRestrictedApplicationResourceReadRequest, ICallable;
