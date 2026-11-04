using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;

namespace Bdgrz.Compliance.Features.Evidence.Queries;

/// <summary>Lists active and withdrawn support by default; status may select active or withdrawn.</summary>
[Discriminator("bdgrz.evidence.supports.list", 1)]
public sealed record ListEvidenceSupports(Uuid TenantId, Uuid ProgramId, string? Status = null,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<EvidenceSupportStatusView>>, IProgramReadRequest, ICallable;
