using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.application_import.get", 1)]
public sealed record GetApplicationImport(Uuid TenantId, Uuid BatchId,
    long? MinimumRevision = null)
    : IRequest<ApplicationImportView>, IApplicationInventoryRequest, ICallable;
