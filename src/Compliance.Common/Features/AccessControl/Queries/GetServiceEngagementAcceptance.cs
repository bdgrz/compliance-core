using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.service-engagement.acceptance.get", 1)]
public sealed record GetServiceEngagementAcceptance(Uuid TenantId, Uuid EngagementId)
    : IRequest<ServiceEngagementAcceptanceView>, IIndependenceAdministrationRequest, ICallable;
