using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Queries;

/// <summary>Lists source provenance; restricted values are always redacted. Target filters must be paired.</summary>
[Discriminator("bdgrz.workforce.source.list", 1)]
public sealed record ListWorkforceSourceObservations(Uuid TenantId, string? TargetKind = null,
    Uuid? TargetId = null, int? Limit = null, string? Cursor = null)
    : IRequest<Page<WorkforceSourceView>>, IWorkforceRequest, ICallable;
