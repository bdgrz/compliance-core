using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.service-engagement.management-acknowledgements.get", 1)]
public sealed record GetEngagementManagementAcknowledgements(Uuid TenantId, Uuid EngagementId)
    : IRequest<IReadOnlyList<EngagementManagementAcknowledgementView>>, IIndependenceAdministrationRequest, ICallable;
