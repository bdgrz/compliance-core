using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Queries;

[Discriminator("bdgrz.provider.assurance_reports.list", 1)]
public sealed record ListProviderAssuranceReports(Uuid TenantId, Uuid ProviderId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AssuranceReportView>>, IProviderRequest, ICallable;
