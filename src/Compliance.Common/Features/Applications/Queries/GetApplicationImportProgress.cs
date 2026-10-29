using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.application_import.progress", 1)]
public sealed record GetApplicationImportProgress(Uuid TenantId, Uuid BatchId, long? MinimumRevision = null)
    : IRequest<ApplicationImportProgress>, IApplicationInventoryRequest, ICallable;
