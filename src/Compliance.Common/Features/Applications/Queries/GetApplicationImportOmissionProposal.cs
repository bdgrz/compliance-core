using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Applications.Queries;

/// <summary>Reads only the still-current durable proposal; no unrestricted historical read is implied.</summary>
[Discriminator("bdgrz.application_import.omission.get", 1)]
public sealed record GetApplicationImportOmissionProposal(Uuid TenantId, Uuid BatchId)
    : IRequest<ApplicationImportOmissionProposalView>, IApplicationInventoryRequest, ICallable;
