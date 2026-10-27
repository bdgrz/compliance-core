using Cntryl.Portia;
using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Examinations.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.examinations.list-audit-observations", 1)]
public sealed record ListAuditObservations(Uuid TenantId, Uuid ProgramId, Uuid ExaminationEngagementId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AuditObservationView>>, IProgramReadRequest, ICallable;
