using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.application_import.progress", 1)]
public sealed record GetApplicationImportProgress(Uuid TenantId, Uuid BatchId, long? MinimumRevision = null)
    : IRequest<ApplicationImportProgress>, IApplicationInventoryRequest, ICallable;
