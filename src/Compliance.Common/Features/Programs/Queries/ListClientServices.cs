using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Queries;

[Discriminator("bdgrz.client-service.list", 1)]
public sealed record ListClientServices(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ClientServiceView>>, IProgramManagementRequest, ICallable;
