using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.system_instance.access_review_scope.get", 1)]
public sealed record GetAccessReviewScope(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, DateTimeOffset? AsOf = null)
    : IRequest<AccessReviewScopeView>, IRestrictedApplicationResourceReadRequest, ICallable;
