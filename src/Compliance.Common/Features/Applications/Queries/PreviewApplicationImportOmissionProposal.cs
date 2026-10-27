using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.application_import.omission.preview", 1)]
public sealed record PreviewApplicationImportOmissionProposal(Uuid TenantId, Uuid BatchId)
    : IRequest<ApplicationImportOmissionPreview>, IApplicationInventoryRequest, ICallable;
