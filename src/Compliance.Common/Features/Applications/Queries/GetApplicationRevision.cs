using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.application.revision.get", 1)]
public sealed record GetApplicationRevision(Uuid TenantId, Uuid ApplicationId, long Revision)
    : IRequest<ApplicationRevisionView>, IRestrictedApplicationResourceReadRequest, ICallable;
