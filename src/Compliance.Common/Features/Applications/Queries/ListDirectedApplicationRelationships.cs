using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.application.relationships.list", 1)]
public sealed record ListDirectedApplicationRelationships(Uuid TenantId,
    Uuid ApplicationId, string Direction, int Limit = 50, string? Cursor = null)
    : IRequest<Page<ApplicationRelationshipView>>, IApplicationInventoryRequest, ICallable;
