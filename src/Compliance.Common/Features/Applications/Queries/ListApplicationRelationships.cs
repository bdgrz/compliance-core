using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.application.relationship.list", 1)]
public sealed record ListApplicationRelationships(Uuid TenantId, Uuid ApplicationId,
    int? Limit = null, string? Cursor = null, long? MinimumRevision = null)
    : IRequest<ApplicationRelationshipPage>, IApplicationInventoryRequest, ICallable;
