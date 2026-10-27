using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.service-engagement.partner-review-context.get", 1)]
public sealed record GetServiceEngagementPartnerReviewContext(Uuid TenantId, Uuid EngagementId)
    : IRequest<ServiceEngagementPartnerReviewContextView>, IPersonalEngagementPartnerRequest, ICallable;
