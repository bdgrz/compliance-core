using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Evaluations.Queries;

/// <summary>Lists a control's evaluations, newest first, optionally filtered by state.</summary>
[Discriminator("bdgrz.control.evaluations.list", 1)]
public sealed record ListControlEvaluations(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string? State = null, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ControlEvaluationView>>, IProgramReadRequest, ICallable;
