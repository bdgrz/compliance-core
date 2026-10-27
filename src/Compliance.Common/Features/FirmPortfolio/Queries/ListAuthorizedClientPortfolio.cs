using Cntryl.Portia;
using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.FirmPortfolio.Models;
namespace Bdgrz.Compliance.Features.FirmPortfolio.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.firm-portfolio.list-authorized-client-portfolio", 1)]
public sealed record ListAuthorizedClientPortfolio(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AuthorizedClientSummaryView>>, ITenantAccessRequest, ICallable;
