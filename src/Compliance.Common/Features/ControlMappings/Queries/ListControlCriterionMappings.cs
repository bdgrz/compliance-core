using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.ControlMappings.Queries;

/// <summary>Lists mappings in a program, optionally for one control, edition, or status.</summary>
[Discriminator("bdgrz.control_mappings.list", 1)]
public sealed record ListControlCriterionMappings(Uuid TenantId, Uuid ProgramId,
    Uuid? ControlId = null, Uuid? EditionId = null, string? Status = null,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<ControlCriterionMappingView>>, IProgramReadRequest, ICallable;
