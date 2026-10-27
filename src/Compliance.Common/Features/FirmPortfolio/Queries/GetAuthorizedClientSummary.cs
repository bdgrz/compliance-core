using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.FirmPortfolio.Models;
namespace Bdgrz.Compliance.Features.FirmPortfolio.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.firm-portfolio.get-authorized-client-summary", 1)]
public sealed record GetAuthorizedClientSummary(Uuid TenantId, Uuid ClientTenantId)
    : IRequest<AuthorizedClientSummaryView>, ITenantAccessRequest, ICallable;
