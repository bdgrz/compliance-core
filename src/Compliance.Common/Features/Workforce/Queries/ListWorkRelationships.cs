using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Queries;

/// <summary>Lists work relationships ordered by source worker ID, with restricted fields redacted.</summary>
[Discriminator("bdgrz.workforce.work-relationship.list", 1)]
public sealed record ListWorkRelationships(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<WorkRelationshipView>>, IWorkforceRequest, ICallable;
