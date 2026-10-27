using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.service-engagement.get", 1)]
public sealed record GetServiceEngagement(Uuid TenantId, Uuid EngagementId)
    : IRequest<ServiceEngagementView>, IIndependenceAdministrationRequest, ICallable;
