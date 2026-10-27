using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.system_instance.access_review_scope.get", 1)]
public sealed record GetAccessReviewScope(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, DateTimeOffset? AsOf = null)
    : IRequest<AccessReviewScopeView>, IRestrictedApplicationResourceReadRequest, ICallable;
