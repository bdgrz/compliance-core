using Cntryl.Portia;
using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.FirmPortfolio.Models;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.FirmPortfolio.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.firm-portfolio.list-my-cross-client-work", 1)]
public sealed record ListMyCrossClientWork(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<CrossClientWorkItemView>>, ITenantAccessRequest, ICallable;
