using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.service-engagement.partner-review-context.get", 1)]
public sealed record GetServiceEngagementPartnerReviewContext(Uuid TenantId, Uuid EngagementId)
    : IRequest<ServiceEngagementPartnerReviewContextView>, IPersonalEngagementPartnerRequest, ICallable;
