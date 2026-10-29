using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.application_import.rejected_report", 1)]
public sealed record GetApplicationImportRejectedReport(Uuid TenantId, Uuid BatchId, long? MinimumRevision = null)
    : IRequest<ApplicationImportRejectedReport>, IApplicationInventoryRequest, ICallable;
